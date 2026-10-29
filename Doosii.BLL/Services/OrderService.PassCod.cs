using System.Data;
using Doosii.BLL.DTOs;
using Doosii.DAL.Models.Forum;
using Doosii.DAL.Models.Order;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Doosii.BLL.Services;

public partial class OrderService
{
    private async Task<Post> PurchasablePassAsync(int buyerId, int postId)
    {
        var buyer = await _context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == buyerId);
        if (buyer == null || buyer.Role is not ("CasualUser" or "Shopper")) throw new UnauthorizedAccessException("Tài khoản không được mua hàng.");
        var post = await _context.Posts.Include(p => p.Author).Include(p => p.PassInfo).Include(p => p.Media).SingleOrDefaultAsync(p => p.Id == postId && p.Type == "PASS") ?? throw new KeyNotFoundException("Không tìm thấy bài PASS.");
        if (post.AuthorId == buyerId) throw new InvalidOperationException("Bạn không thể mua món của mình.");
        if (post.VisibilityStatus != "ACTIVE" || post.HiddenByAdmin || post.Author.Role != "CasualUser" || post.PassInfo?.SaleStatus != "AVAILABLE") throw new InvalidOperationException("Món PASS không còn được bán.");
        if (await _context.Set<PassOrderLock>().AnyAsync(l => l.PostId == postId)) throw new InvalidOperationException("Món PASS đang được giữ cho đơn khác.");
        return post;
    }
    private OrderQuoteResponse PassQuote(Post post)
    {
        var shipping = _configuration.GetValue<decimal?>("Orders:ShippingFee") ?? 30000m;
        if (shipping < 0 || shipping != decimal.Truncate(shipping)) throw new InvalidOperationException("Phí vận chuyển trên máy chủ không hợp lệ.");
        return new OrderQuoteResponse { PassPostId = post.Id, SellerId = post.AuthorId, ProductName = post.PassInfo!.ProductName,
            Thumbnail = post.Media.OrderBy(m => m.DisplayOrder).Where(m => m.MediaType == "IMAGE").Select(m => m.SourceUrl).FirstOrDefault(),
            ProductVersion = Convert.ToBase64String(post.RowVersion), ProductPrice = post.PassInfo.Price, ShippingFee = shipping,
            TotalAmount = post.PassInfo.Price + shipping };
    }
    public async Task<OrderQuoteResponse> QuotePassCodAsync(int buyerId, int postId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        await CommunityAccess.LockAsync(_context, $"community:post:{postId}");
        var quote = PassQuote(await PurchasablePassAsync(buyerId, postId)); await tx.CommitAsync(); return quote;
    }
    public async Task<OrderResponse> CreatePassCodAsync(int buyerId, CreatePassCodRequest request)
    {
        if (request.ClientRequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.ReceiverName) || string.IsNullOrWhiteSpace(request.ReceiverPhone) || string.IsNullOrWhiteSpace(request.ShippingAddress)) throw new InvalidOperationException("Thông tin đơn hàng không hợp lệ.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _context.Users.FromSqlInterpolated($"SELECT * FROM [auth].[Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {buyerId}").LoadAsync();
        var buyer = await _context.Users.SingleOrDefaultAsync(u => u.Id == buyerId);
        if (buyer == null || buyer.Role is not ("CasualUser" or "Shopper")) throw new UnauthorizedAccessException("Tài khoản không được mua hàng.");
        var existing = await _context.Orders.Include(o => o.Items).Include(o => o.Buyer).Include(o => o.Seller).SingleOrDefaultAsync(o => o.BuyerId == buyerId && o.ClientRequestId == request.ClientRequestId);
        if (existing != null)
        {
            var item = existing.Items.SingleOrDefault();
            if (item?.SourceType != "PASS_POST" || item.PassPostId != request.PassPostId || existing.PaymentMethod != "COD" || existing.ReceiverName != request.ReceiverName.Trim() || existing.ReceiverPhone != request.ReceiverPhone.Trim() || existing.ShippingAddress != request.ShippingAddress.Trim() || Convert.ToBase64String(item.SourceVersionSnapshot ?? Array.Empty<byte>()) != request.ProductVersion) throw new InvalidOperationException("Mã yêu cầu đã dùng cho nội dung khác.");
            await tx.CommitAsync(); return MapToOrderResponse(existing, existing.Buyer.FullName, existing.Seller.FullName);
        }
        await CommunityAccess.LockAsync(_context, $"community:post:{request.PassPostId}");
        var post = await PurchasablePassAsync(buyerId, request.PassPostId);
        if (Convert.ToBase64String(post.RowVersion) != request.ProductVersion) throw new InvalidOperationException("Bài PASS đã thay đổi. Hãy tải lại báo giá.");
        var quote = PassQuote(post);
        var order = new Order { BuyerId = buyerId, SellerId = post.AuthorId, PaymentMethod = "COD", Status = OrderStatus.CodPending,
            ProductPrice = quote.ProductPrice, ShippingFee = quote.ShippingFee, EscrowFee = 0, TotalAmount = quote.TotalAmount,
            ClientRequestId = request.ClientRequestId, ReceiverName = request.ReceiverName.Trim(), ReceiverPhone = request.ReceiverPhone.Trim(), ShippingAddress = request.ShippingAddress.Trim(),
            Items = new List<OrderItem> { new() { SourceType = "PASS_POST", PassPostId = post.Id, ProductNameSnapshot = quote.ProductName, PriceSnapshot = quote.ProductPrice,
                ThumbnailSnapshot = quote.Thumbnail, SellerNameSnapshot = post.Author.FullName, SourceVersionSnapshot = post.RowVersion.ToArray() } } };
        _context.Orders.Add(order); await _context.SaveChangesAsync();
        _context.Set<PassOrderLock>().Add(new PassOrderLock { PostId = post.Id, OrderId = order.Id });
        post.UpdatedAt = DateTime.UtcNow; await _context.SaveChangesAsync(); await tx.CommitAsync();
        return await GetOrderByIdAsync(order.Id, buyerId);
    }
    private async Task ClosePassLockAsync(OrderItem item, int orderId, bool sold)
    {
        if (item.PassPostId is not int postId) throw new InvalidOperationException("Nguồn bài PASS không hợp lệ.");
        await CommunityAccess.LockAsync(_context, $"community:post:{postId}");
        var row = await _context.Set<PassOrderLock>().SingleOrDefaultAsync(l => l.PostId == postId && l.OrderId == orderId) ?? throw new InvalidOperationException("Món PASS không còn thuộc đơn này.");
        var post = await _context.Posts.Include(p => p.PassInfo).SingleAsync(p => p.Id == postId);
        if (sold) post.PassInfo!.SaleStatus = "SOLD";
        post.UpdatedAt = DateTime.UtcNow; _context.Set<PassOrderLock>().Remove(row);
    }
}
