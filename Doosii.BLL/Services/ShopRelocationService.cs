using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Doosii.BLL.Hubs;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Store;
using Doosii.DAL.Models.Notification;

namespace Doosii.BLL.Services;

public class ShopRelocationService(AppDbContext db, IHubContext<NotificationHub> hub, ILogger<ShopRelocationService> logger) : IShopRelocationService
{
    private async Task<Store> OwnedStore(int userId, int storeId, bool forWrite = false)
    {
        // Create/cancel serialize on this store row; the filtered unique index is the final backstop.
        var query = forWrite
            ? db.Stores.FromSqlInterpolated($"SELECT * FROM [store].[Stores] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {storeId}")
            : db.Stores.AsNoTracking().Where(s => s.Id == storeId);
        var store = await query.SingleOrDefaultAsync() ?? throw new KeyNotFoundException("Không tìm thấy shop.");
        if (store.OwnerId != userId) throw new ServiceException(403, "Bạn không phải chủ shop.", "NOT_OWNER");
        return store;
    }

    public async Task<ShopRelocationPage> GetAsync(int userId, int storeId, int page, int pageSize)
    {
        if (page < 1 || page > 100000 || pageSize is < 1 or > 50) throw new ArgumentException("Phân trang không hợp lệ.");
        await OwnedStore(userId, storeId);
        var query = db.ShopRelocationRequests.AsNoTracking().Where(r => r.StoreId == storeId);
        var total = await query.CountAsync();
        var pending = await query.SingleOrDefaultAsync(r => r.Status == "PENDING");
        var rows = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new ShopRelocationPage { Items = rows.Select(Map).ToList(), PendingRequest = pending == null ? null : Map(pending), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<ShopRelocationResponse> CreateAsync(int userId, int storeId, CreateShopRelocationRequest request)
    {
        var address = request.ProposedAddress?.Trim() ?? "";
        var reason = request.Reason?.Trim() ?? "";
        if (address.Length is < 1 or > 500 || reason.Length is < 1 or > 1000 ||
            !request.ProposedLatitude.HasValue || !double.IsFinite(request.ProposedLatitude.Value) || request.ProposedLatitude is < -90 or > 90 ||
            !request.ProposedLongitude.HasValue || !double.IsFinite(request.ProposedLongitude.Value) || request.ProposedLongitude is < -180 or > 180 ||
            !request.ClientRequestId.HasValue || request.ClientRequestId == Guid.Empty)
            throw new ArgumentException("Kiểm tra địa chỉ, tọa độ, lý do và mã yêu cầu.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var store = await OwnedStore(userId, storeId, true);
        var existing = await db.ShopRelocationRequests.AsNoTracking().SingleOrDefaultAsync(r => r.StoreId == storeId && r.ClientRequestId == request.ClientRequestId);
        if (existing != null)
        {
            if (existing.ProposedAddress != address || existing.ProposedLatitude != request.ProposedLatitude || existing.ProposedLongitude != request.ProposedLongitude || existing.Reason != reason)
                throw new ServiceException(409, "Mã yêu cầu đã dùng với nội dung khác. Hãy tải lại lịch sử.", "IDEMPOTENCY_CONFLICT");
            await transaction.CommitAsync(); return Map(existing);
        }
        if (!store.IsActive || !await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role == "Shopper" && u.MerchantProfile != null && u.MerchantProfile.KycStatus == "APPROVED"))
            throw new ServiceException(403, "Shop cần đang hoạt động và đã duyệt hồ sơ để yêu cầu đổi vị trí.", "INACTIVE_SHOP");
        if (await db.ShopRelocationRequests.AnyAsync(r => r.StoreId == storeId && r.Status == "PENDING"))
            throw new ServiceException(409, "Shop đã có yêu cầu đang chờ duyệt. Hãy xem hoặc hủy yêu cầu đó trước.", "PENDING_REQUEST_EXISTS");
        var location = await db.StoreLocations.AsNoTracking().Where(l => l.StoreId == storeId).OrderBy(l => l.Id).FirstOrDefaultAsync();
        var currentLat = store.Latitude ?? location?.Latitude;
        var currentLng = store.Longitude ?? location?.Longitude;
        if (currentLat == request.ProposedLatitude && currentLng == request.ProposedLongitude)
            throw new ArgumentException("Hãy chọn vị trí mới khác vị trí hiện tại.");
        var row = new ShopRelocationRequest {
            StoreId = storeId, RequestedByUserId = userId, ClientRequestId = request.ClientRequestId.Value,
            CurrentAddress = store.Address ?? location?.Address, CurrentLatitude = currentLat, CurrentLongitude = currentLng,
            ProposedAddress = address, ProposedLatitude = request.ProposedLatitude.Value, ProposedLongitude = request.ProposedLongitude.Value,
            Reason = reason, Status = "PENDING", CreatedAt = DateTime.UtcNow,
        };
        db.ShopRelocationRequests.Add(row); await db.SaveChangesAsync(); await transaction.CommitAsync();
        return Map(row);
    }

    public async Task<ShopRelocationResponse> CancelAsync(int userId, int storeId, int requestId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await OwnedStore(userId, storeId, true);
        var row = await db.ShopRelocationRequests.SingleOrDefaultAsync(r => r.StoreId == storeId && r.Id == requestId)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu của shop.");
        if (row.Status != "PENDING" && row.Status != "CANCELLED")
            throw new ServiceException(409, "Chỉ có thể hủy yêu cầu đang chờ duyệt.", "REQUEST_ALREADY_REVIEWED");
        if (row.Status == "PENDING")
        {
            row.Status = "CANCELLED"; row.CancelledAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync(); return Map(row);
    }

    private async Task<Doosii.DAL.Models.User> EnsureAdmin(int adminId) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == adminId && u.Role == "Admin")
        ?? throw new ServiceException(403, "Chỉ admin hiện tại mới được xét duyệt yêu cầu.", "ADMIN_REQUIRED");

    private IQueryable<ShopRelocationRequest> AdminQuery() => db.ShopRelocationRequests.AsNoTracking()
        .Include(r => r.Store).ThenInclude(s => s.Locations)
        .Include(r => r.Store).ThenInclude(s => s.Owner).ThenInclude(u => u.MerchantProfile);

    public async Task<AdminShopRelocationPage> AdminListAsync(int adminId, string? status, int page, int pageSize)
    {
        await EnsureAdmin(adminId);
        if (page < 1 || page > 100000 || pageSize is < 1 or > 50) throw new ArgumentException("Phân trang không hợp lệ.");
        if (status != null && status is not ("PENDING" or "APPROVED" or "REJECTED" or "CANCELLED")) throw new ArgumentException("Trạng thái không hợp lệ.");
        var query = AdminQuery();
        if (status != null) query = query.Where(r => r.Status == status);
        var total = await query.CountAsync();
        var pending = await db.ShopRelocationRequests.CountAsync(r => r.Status == "PENDING");
        var rows = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new AdminShopRelocationPage { Items = rows.Select(AdminMap).ToList(), TotalCount = total, PendingCount = pending, Page = page, PageSize = pageSize };
    }

    public async Task<AdminShopRelocationResponse> AdminDetailAsync(int adminId, int requestId)
    {
        await EnsureAdmin(adminId);
        return AdminMap(await AdminQuery().SingleOrDefaultAsync(r => r.Id == requestId)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu đổi vị trí."));
    }

    private static (string? Address, double? Lat, double? Lng) LiveLocation(Store store)
    {
        var location = store.Locations.OrderBy(l => l.Id).FirstOrDefault();
        return (store.Address ?? location?.Address, store.Latitude ?? location?.Latitude, store.Longitude ?? location?.Longitude);
    }

    private static string? ApprovalBlock(ShopRelocationRequest row)
    {
        if (row.Status != "PENDING") return "Yêu cầu không còn chờ duyệt.";
        var store = row.Store;
        if (!store.IsActive || store.Owner.Role != "Shopper" || store.Owner.MerchantProfile?.KycStatus != "APPROVED") return "Shop không còn hoạt động hoặc chưa đủ điều kiện KYC.";
        if (store.OwnerId != row.RequestedByUserId) return "Chủ sở hữu shop đã thay đổi. Hãy từ chối và yêu cầu chủ shop hiện tại gửi lại.";
        var live = LiveLocation(store);
        if (live.Address != row.CurrentAddress || live.Lat != row.CurrentLatitude || live.Lng != row.CurrentLongitude) return "Vị trí hiện tại đã thay đổi so với lúc gửi. Hãy từ chối và yêu cầu shop gửi lại.";
        return null;
    }

    private static AdminShopRelocationResponse AdminMap(ShopRelocationRequest row)
    {
        var live = LiveLocation(row.Store);
        var block = ApprovalBlock(row);
        return new AdminShopRelocationResponse {
            Request = Map(row), StoreName = row.Store.Name, OwnerId = row.Store.OwnerId, OwnerName = row.Store.Owner.FullName,
            LiveAddress = live.Address, LiveLatitude = live.Lat, LiveLongitude = live.Lng,
            CanApprove = block == null, ApprovalBlockReason = block,
        };
    }

    public async Task<AdminShopRelocationResponse> ReviewAsync(int adminId, int requestId, ReviewShopRelocationRequest request)
    {
        await EnsureAdmin(adminId);
        var reason = request.Reason?.Trim() ?? "";
        if (request.Decision is not ("APPROVE" or "REJECT") || reason.Length is < 1 or > 1000 || !request.ClientRequestId.HasValue || request.ClientRequestId == Guid.Empty)
            throw new ArgumentException("Chọn duyệt hoặc từ chối, nhập lý do tối đa 1000 ký tự và mã yêu cầu hợp lệ.");
        var storeId = await db.ShopRelocationRequests.AsNoTracking().Where(r => r.Id == requestId).Select(r => (int?)r.StoreId).SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu đổi vị trí.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Use the same store lock as create/cancel. A cancelled/reviewed request cannot win a second transition.
        var store = await db.Stores.FromSqlInterpolated($"SELECT * FROM [store].[Stores] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {storeId}").SingleAsync();
        var admin = await EnsureAdmin(adminId);
        await db.Entry(store).Collection(s => s.Locations).LoadAsync();
        await db.Entry(store).Reference(s => s.Owner).LoadAsync();
        await db.Entry(store.Owner).Reference(u => u.MerchantProfile).LoadAsync();
        var row = await db.ShopRelocationRequests.SingleOrDefaultAsync(r => r.Id == requestId && r.StoreId == storeId)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu đổi vị trí.");
        var outcome = request.Decision == "APPROVE" ? "APPROVED" : "REJECTED";
        if (row.Status != "PENDING")
        {
            if (row.Status == outcome && row.ReviewedByUserId == adminId && row.ReviewRequestId == request.ClientRequestId && row.ReviewReason == reason)
            { await transaction.CommitAsync(); return AdminMap(row); }
            throw new ServiceException(409, "Yêu cầu đã hủy hoặc đã được xử lý. Hãy tải lại kết quả.", "REQUEST_ALREADY_RESOLVED");
        }
        if (request.Decision == "APPROVE")
        {
            var block = ApprovalBlock(row);
            if (block != null) throw new ServiceException(409, block, "APPROVAL_BLOCKED");
            store.Address = row.ProposedAddress; store.Latitude = row.ProposedLatitude; store.Longitude = row.ProposedLongitude;
            store.UpdatedAt = DateTime.UtcNow;
            // All location rows in this MVP mirror the shop's public map location. Preserve their IDs.
            if (store.Locations.Count == 0) store.Locations.Add(new StoreLocation { StoreId = storeId });
            foreach (var location in store.Locations) {
                location.Address = row.ProposedAddress; location.Latitude = row.ProposedLatitude; location.Longitude = row.ProposedLongitude;
            }
            var pickupNow = DateTime.UtcNow;
            var pickups = await db.PickupReservations.AsNoTracking().Where(r => r.StoreId == store.Id && r.Status == "ACCEPTED" && r.ExpiresAt > pickupNow).ToListAsync();
            foreach (var pickup in pickups) PickupNotifications.Location(db, pickup, row.Id, pickupNow);
        }
        row.Status = outcome; row.ReviewedAt = DateTime.UtcNow; row.ReviewedByUserId = adminId;
        row.ReviewedByName = admin.FullName; row.ReviewReason = reason; row.ReviewRequestId = request.ClientRequestId;
        var notification = new Notification {
            UserId = store.OwnerId, Title = outcome == "APPROVED" ? "Yêu cầu đổi vị trí shop đã được duyệt" : "Yêu cầu đổi vị trí shop bị từ chối",
            Message = $"Yêu cầu #{row.Id}. {(outcome == "APPROVED" ? "Vị trí mới đã được áp dụng." : "Vị trí hiện tại được giữ nguyên.")} Lý do: {reason[..Math.Min(reason.Length, 800)]}",
            Type = "SHOP_RELOCATION", TargetUrl = "/shop-management/address-change", CreatedAt = DateTime.UtcNow,
        };
        // Persist notification in the same transaction as the verdict/location, then push only after commit.
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        try {
            await hub.Clients.Group($"user_{notification.UserId}").SendAsync("ReceiveNotification", new NotificationDto {
                Id = notification.Id, UserId = notification.UserId, Title = notification.Title, Message = notification.Message,
                Type = notification.Type, TargetUrl = notification.TargetUrl, IsRead = false, CreatedAt = notification.CreatedAt,
            });
        } catch (Exception ex) { logger.LogWarning(ex, "Relocation {Id} saved; realtime notification unavailable", row.Id); }
        return AdminMap(row);
    }

    private static ShopRelocationResponse Map(ShopRelocationRequest r) => new() {
        Id = r.Id, StoreId = r.StoreId, CurrentAddress = r.CurrentAddress, CurrentLatitude = r.CurrentLatitude, CurrentLongitude = r.CurrentLongitude,
        ProposedAddress = r.ProposedAddress, ProposedLatitude = r.ProposedLatitude, ProposedLongitude = r.ProposedLongitude,
        Reason = r.Reason, Status = r.Status, CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
        CancelledAt = r.CancelledAt.HasValue ? DateTime.SpecifyKind(r.CancelledAt.Value, DateTimeKind.Utc) : null,
        ReviewedByUserId = r.ReviewedByUserId, ReviewedByName = r.ReviewedByName, ReviewReason = r.ReviewReason,
        ReviewedAt = r.ReviewedAt.HasValue ? DateTime.SpecifyKind(r.ReviewedAt.Value, DateTimeKind.Utc) : null,
    };
}
