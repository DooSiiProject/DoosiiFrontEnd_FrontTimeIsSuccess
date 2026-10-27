using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;

namespace Doosii.BLL.Services;

public class StoreReviewService : IStoreReviewService
{
    private readonly AppDbContext _context;
    public StoreReviewService(AppDbContext context) { _context = context; }

    private static ServiceException Failure(int status, string message, string code) => new(status, message, code);
    private static bool Public(Store store) => store.IsActive && store.Owner.Role == "Shopper" && store.Owner.MerchantProfile?.KycStatus == "APPROVED";
    private async Task<Store> StoreAsync(int id, bool locked = false)
    {
        IQueryable<Store> query = locked
            ? _context.Stores.FromSqlInterpolated($"SELECT * FROM [store].[Stores] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            : _context.Stores.Where(s => s.Id == id);
        return await query.Include(s => s.Owner).ThenInclude(u => u.MerchantProfile).SingleOrDefaultAsync()
            ?? throw Failure(404, "Không tìm thấy shop.", "STORE_NOT_FOUND");
    }
    private async Task EnsureUserAsync(int id)
    {
        if (!await _context.Users.AnyAsync(u => u.Id == id && u.Role != "Banned")) throw Failure(403, "Tài khoản không được phép đánh giá.", "FORBIDDEN");
    }
    private IQueryable<Doosii.DAL.Models.Order.Order> EligibleOrders(int userId, Store store) => _context.Orders.AsNoTracking()
        .Where(o => o.BuyerId == userId && o.SellerId == store.OwnerId && (o.Status == OrderStatus.CompletedReleased || o.Status == OrderStatus.CodCompleted)
            && o.Items.Any(i => _context.Products.Any(p => p.Id == i.ProductId && p.StoreId == store.Id)));
    private static void Validate(int rating, string? comment, List<string>? images)
    {
        if (rating < 1 || rating > 5) throw Failure(400, "Điểm đánh giá phải từ 1 đến 5 sao.", "INVALID_RATING");
        if (comment?.Length > 2000) throw Failure(400, "Nhận xét tối đa 2000 ký tự.", "COMMENT_TOO_LONG");
        if (images?.Count > 3) throw Failure(400, "Đánh giá tối đa 3 ảnh.", "MAX_3_IMAGES");
        if (images != null && (images.Any(url => string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")) || JsonSerializer.Serialize(images).Length > 2000))
            throw Failure(400, "Đường dẫn ảnh không hợp lệ hoặc quá dài.", "INVALID_IMAGES");
    }
    private static StoreReviewDto Map(StoreReview review) => new() {
        ReviewId = review.Id, UserId = review.UserId, UserDisplayName = review.User?.FullName ?? "",
        UserAvatarUrl = review.User?.AvatarUrl, Rating = review.Rating, Comment = review.Comment,
        Images = string.IsNullOrEmpty(review.ImagesJson) ? new() : JsonSerializer.Deserialize<List<string>>(review.ImagesJson) ?? new(),
        CreatedAt = DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Utc),
        UpdatedAt = review.UpdatedAt.HasValue ? DateTime.SpecifyKind(review.UpdatedAt.Value, DateTimeKind.Utc) : null
    };
    private static void Apply(StoreReview review, int rating, string? comment, List<string>? images)
    {
        review.Rating = rating; review.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        review.ImagesJson = images?.Count > 0 ? JsonSerializer.Serialize(images) : null;
    }
    // Every writer locks the shop row first, so review mutations and cached rating stay atomic.
    private async Task RecalculateAsync(Store store)
    {
        var stats = await _context.StoreReviews.Where(r => r.StoreId == store.Id).GroupBy(r => r.StoreId)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (decimal)r.Rating) }).SingleOrDefaultAsync();
        store.RatingCount = stats?.Count ?? 0;
        store.RatingAverage = stats == null ? 0 : Math.Round(stats.Average, 2, MidpointRounding.AwayFromZero);
        await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context);
    }
    public async Task<StoreReviewDto> CreateReviewAsync(int userId, int storeId, CreateStoreReviewRequest request)
    {
        Validate(request.Rating, request.Comment, request.Images);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var store = await StoreAsync(storeId, true); await EnsureUserAsync(userId);
        if (!Public(store)) throw Failure(403, "Shop không còn đủ điều kiện hoạt động.", "STORE_NOT_ACTIVE");
        if (store.OwnerId == userId) throw Failure(400, "Bạn không thể đánh giá shop của mình.", "CANNOT_REVIEW_OWN_STORE");
        if (await _context.StoreReviews.AnyAsync(r => r.StoreId == storeId && r.UserId == userId)) throw Failure(409, "Bạn đã đánh giá shop này. Hãy sửa đánh giá hiện có.", "REVIEW_ALREADY_EXISTS");
        var eligible = EligibleOrders(userId, store);
        if (request.OrderId.HasValue) eligible = eligible.Where(o => o.Id == request.OrderId.Value);
        var orderId = await eligible.OrderByDescending(o => o.CompletedAt ?? o.CreatedAt).ThenByDescending(o => o.Id).Select(o => (int?)o.Id).FirstOrDefaultAsync();
        if (orderId == null) throw Failure(400, "Bạn cần hoàn tất đơn hàng tại shop này trước khi đánh giá.", "NO_COMPLETED_ORDER");
        var review = new StoreReview { StoreId = storeId, UserId = userId, OrderId = orderId, CreatedAt = DateTime.UtcNow };
        Apply(review, request.Rating, request.Comment, request.Images);
        _context.StoreReviews.Add(review); await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); await RecalculateAsync(store); await tx.CommitAsync();
        await _context.Entry(review).Reference(r => r.User).LoadAsync(); return Map(review);
    }
    public async Task<StoreReviewDto> UpdateReviewAsync(int userId, int storeId, int reviewId, UpdateStoreReviewRequest request)
    {
        Validate(request.Rating, request.Comment, request.Images);
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var store = await StoreAsync(storeId, true); await EnsureUserAsync(userId);
        var review = await _context.StoreReviews.Include(r => r.User).SingleOrDefaultAsync(r => r.Id == reviewId && r.StoreId == storeId)
            ?? throw Failure(404, "Không tìm thấy đánh giá.", "REVIEW_NOT_FOUND");
        if (review.UserId != userId) throw Failure(403, "Bạn chỉ được sửa đánh giá của mình.", "FORBIDDEN");
        Apply(review, request.Rating, request.Comment, request.Images); review.UpdatedAt = DateTime.UtcNow;
        await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); await RecalculateAsync(store); await tx.CommitAsync(); return Map(review);
    }
    public async Task DeleteReviewAsync(int userId, bool isAdmin, int storeId, int reviewId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var store = await StoreAsync(storeId, true); await EnsureUserAsync(userId);
        var review = await _context.StoreReviews.SingleOrDefaultAsync(r => r.Id == reviewId && r.StoreId == storeId)
            ?? throw Failure(404, "Không tìm thấy đánh giá của shop này.", "REVIEW_NOT_FOUND");
        var admin = isAdmin && await _context.Users.AnyAsync(u => u.Id == userId && u.Role == "Admin");
        if (review.UserId != userId && !admin) throw Failure(403, "Bạn không có quyền xóa đánh giá này.", "FORBIDDEN");
        _context.StoreReviews.Remove(review); await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); await RecalculateAsync(store); await tx.CommitAsync();
    }
    public async Task<StoreReviewsSummaryDto> GetReviewsAsync(int storeId, StoreReviewQuery query)
    {
        var store = await StoreAsync(storeId);
        if (!Public(store)) throw Failure(404, "Không tìm thấy shop đang hoạt động.", "STORE_NOT_ACTIVE");
        if (query.Page < 1 || query.Page > 1000000 || query.PageSize < 1 || query.PageSize > 50 || !new[] { "newest", "highest", "lowest" }.Contains(query.SortBy))
            throw Failure(400, "Phân trang hoặc sắp xếp không hợp lệ.", "INVALID_QUERY");
        var reviews = _context.StoreReviews.AsNoTracking().Where(r => r.StoreId == storeId);
        var count = await reviews.CountAsync();
        // Compute the summary from the same source of truth, including legacy reviews.
        var average = await reviews.Select(r => (decimal?)r.Rating).AverageAsync() ?? 0;
        var ordered = query.SortBy switch {
            "highest" => reviews.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id),
            "lowest" => reviews.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id),
            _ => reviews.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
        };
        var items = await ordered.Include(r => r.User).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();
        return new StoreReviewsSummaryDto { StoreId = storeId, RatingAverage = Math.Round(average, 2, MidpointRounding.AwayFromZero), TotalReviews = count, Page = query.Page, PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)count / query.PageSize), Items = items.Select(Map).ToList() };
    }
    public async Task<MyStoreReviewDto> GetMyReviewAsync(int userId, int storeId, int? orderId = null)
    {
        await EnsureUserAsync(userId);
        var store = await StoreAsync(storeId);
        var review = await _context.StoreReviews.AsNoTracking().Include(r => r.User).SingleOrDefaultAsync(r => r.StoreId == storeId && r.UserId == userId);
        var result = new MyStoreReviewDto { Review = review == null ? null : Map(review) };
        if (review != null) return result;
        if (store.OwnerId == userId) { result.Reason = "Bạn không thể đánh giá shop của mình."; return result; }
        if (!Public(store)) { result.Reason = "Shop không còn đủ điều kiện nhận đánh giá mới."; return result; }
        var eligible = EligibleOrders(userId, store);
        if (orderId.HasValue) eligible = eligible.Where(o => o.Id == orderId);
        result.EligibleOrderId = await eligible.OrderByDescending(o => o.CompletedAt ?? o.CreatedAt).ThenByDescending(o => o.Id).Select(o => (int?)o.Id).FirstOrDefaultAsync();
        result.CanCreate = result.EligibleOrderId.HasValue;
        result.Reason = result.CanCreate ? "" : "Bạn cần hoàn tất đơn hàng tại shop này trước khi đánh giá.";
        return result;
    }
    public async Task<List<OrderReviewShopDto>> GetOrderReviewShopsAsync(int userId, int orderId)
    {
        await EnsureUserAsync(userId);
        if (!await _context.Orders.AnyAsync(o => o.Id == orderId && o.BuyerId == userId && (o.Status == OrderStatus.CompletedReleased || o.Status == OrderStatus.CodCompleted)))
            throw Failure(404, "Không tìm thấy đơn hàng hoàn tất của bạn.", "NO_COMPLETED_ORDER");
        return await (from item in _context.OrderItems.AsNoTracking()
            join product in _context.Products on item.ProductId equals product.Id
            where item.OrderId == orderId
            select new OrderReviewShopDto { StoreId = product.StoreId, StoreName = product.Store.Name }).Distinct().ToListAsync();
    }
}
