using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;
using Microsoft.EntityFrameworkCore;

namespace Doosii.BLL.Services;

public sealed partial class PickupReservationService(AppDbContext db, TimeProvider clock, PickupCodeProtection codes) : IPickupReservationService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static ServiceException Error(int status, string code, string message) => new(status, message, code);
    private static ServiceException Missing() => Error(404, "RESERVATION_NOT_FOUND", "Không tìm thấy lượt giữ.");
    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw Error(400, "INVALID_REQUEST", string.Join(" ", errors.Select(e => e.ErrorMessage)));
    }
    private async Task<User> Actor(int id)
    {
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id)
            ?? throw Error(401, "UNAUTHORIZED", "Vui lòng đăng nhập.");
        if (actor.Role is not ("CasualUser" or "Shopper")) throw Error(403, "ROLE_NOT_ALLOWED", "Tài khoản không có quyền giữ đồ.");
        return actor;
    }
    private async Task<Product> LockProduct(int id) => await db.Products.FromSqlInterpolated(
        $"SELECT * FROM [store].[Products] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync()
        ?? throw Error(409, "PRODUCT_UNAVAILABLE", "Sản phẩm không còn khả dụng.");
    private async Task<Store> StoreFor(Product product)
    {
        var shop = await db.Stores.FromSqlInterpolated($"SELECT * FROM [store].[Stores] WITH (HOLDLOCK) WHERE [Id]={product.StoreId}").SingleAsync();
        await db.Entry(shop).ReloadAsync();
        await db.Users.FromSqlInterpolated($"SELECT * FROM [auth].[Users] WITH (HOLDLOCK) WHERE [Id]={shop.OwnerId}").LoadAsync();
        await db.MerchantProfiles.FromSqlInterpolated($"SELECT * FROM [auth].[MerchantProfiles] WITH (HOLDLOCK) WHERE [UserId]={shop.OwnerId}").LoadAsync();
        await db.Entry(shop).Reference(s => s.Owner).LoadAsync();
        await db.Entry(shop.Owner).ReloadAsync();
        await db.Entry(shop.Owner).Reference(u => u.MerchantProfile).LoadAsync();
        if (shop.Owner.MerchantProfile != null) await db.Entry(shop.Owner.MerchantProfile).ReloadAsync();
        await db.Entry(shop).Collection(s => s.Locations).LoadAsync();
        return shop;
    }
    private static void Eligible(Store shop, Product product)
    {
        if (!shop.IsActive || shop.Owner.Role != "Shopper" || shop.Owner.MerchantProfile?.KycStatus != "APPROVED" || !product.IsPublished)
            throw Error(403, "STORE_NOT_ELIGIBLE", "Shop hoặc sản phẩm không còn được công khai để giữ.");
    }
    private static void Available(Product product)
    {
        if (product.Status != "AVAILABLE" || product.ReservedOrderId != null || product.ReservedPickupReservationId != null)
            throw Error(409, "PRODUCT_UNAVAILABLE", "Sản phẩm đang được giữ hoặc đã bán.");
    }
    private static void Version(PickupReservation row, string version)
    {
        if (Convert.ToBase64String(row.RowVersion) != version) throw Error(409, "STALE_VERSION", "Lượt giữ đã thay đổi. Hãy tải lại.");
    }
    private async Task CleanupProduct(int productId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var product = await LockProduct(productId);
        await PickupReservationLifecycle.ExpireProductAsync(db, product, Now);
        await tx.CommitAsync();
        db.ChangeTracker.Clear();
    }
    private IQueryable<PickupReservation> Visible(int actorId) => db.PickupReservations.AsNoTracking().Where(r =>
        r.BuyerId == actorId || (r.SellerId == actorId && db.Stores.Any(s => s.Id == r.StoreId && s.OwnerId == actorId)));
    private async Task<PickupReservation> Read(int actorId, int id) => await Visible(actorId).SingleOrDefaultAsync(r => r.Id == id) ?? throw Missing();
    public async Task<PickupCreateResult> CreateAsync(int actorId, CreatePickupReservationRequest request)
    {
        Validate(request); await Actor(actorId);
        request.ContactName = request.ContactName.Trim(); request.ContactPhone = request.ContactPhone.Trim();
        request.BuyerNote = string.IsNullOrWhiteSpace(request.BuyerNote) ? null : request.BuyerNote.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            request.ProductId, request.ProductVersion, scheduledAt = request.ScheduledAt.UtcDateTime,
            request.ContactName, request.ContactPhone, request.BuyerNote }))).ToLowerInvariant();
        // Cleanup commits independently: a subsequent stale quote must not resurrect an expired lock.
        await CleanupProduct(request.ProductId);
        await using var tx = await db.Database.BeginTransactionAsync();
        // Per-buyer application lock serializes quota/retry across products without taking
        // a user row update lock that could deadlock against that shopper's own sales.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource={"pickup:buyer:" + actorId}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000;
            IF @result < 0 THROW 51010, 'Pickup request conflict', 1;
            """);
        await Actor(actorId);
        var existing = await db.PickupReservations.SingleOrDefaultAsync(r => r.BuyerId == actorId && r.ClientRequestId == request.ClientRequestId);
        if (existing != null)
        {
            if (existing.RequestHash != hash) throw Error(409, "IDEMPOTENCY_CONFLICT", "Mã yêu cầu đã dùng với nội dung khác.");
            await tx.CommitAsync(); return new(await Map(existing), true);
        }
        var product = await LockProduct(request.ProductId);
        var now = Now;
        if (await PickupReservationLifecycle.ExpireProductAsync(db, product, now))
        {
            await tx.CommitAsync(); throw Error(409, "PRODUCT_CHANGED", "Lượt giữ cũ vừa hết hạn. Hãy tải lại sản phẩm.");
        }
        var shop = await StoreFor(product); Eligible(shop, product); Available(product);
        if (shop.OwnerId == actorId) throw Error(403, "ROLE_NOT_ALLOWED", "Bạn không thể giữ sản phẩm của mình.");
        if (Convert.ToBase64String(product.RowVersion) != request.ProductVersion) throw Error(409, "PRODUCT_CHANGED", "Sản phẩm đã thay đổi. Hãy tải lại.");
        now = Now;
        var scheduled = request.ScheduledAt.UtcDateTime;
        if (scheduled < now.AddMinutes(30) || scheduled > now.AddHours(24)) throw Error(400, "INVALID_SCHEDULE", "Chọn giờ đến từ 30 phút đến 24 giờ tới.");
        if (await db.PickupReservations.AnyAsync(r => r.BuyerId == actorId && r.ProductId == product.Id && (r.Status == "PENDING" || r.Status == "ACCEPTED")))
            throw Error(409, "ACTIVE_RESERVATION_EXISTS", "Bạn đã có lượt giữ đang hiệu lực cho món này.");
        if (await db.PickupReservations.CountAsync(r => r.BuyerId == actorId &&
            ((r.Status == "PENDING" && r.ResponseDeadline > now) || (r.Status == "ACCEPTED" && r.ExpiresAt > now))) >= 3)
            throw Error(409, "RESERVATION_LIMIT_REACHED", "Bạn chỉ được có tối đa 3 lượt giữ đang hiệu lực.");
        var row = new PickupReservation { BuyerId = actorId, SellerId = shop.OwnerId, StoreId = shop.Id, ProductId = product.Id,
            ClientRequestId = request.ClientRequestId, RequestHash = hash, ContactName = request.ContactName, ContactPhone = request.ContactPhone,
            BuyerNote = request.BuyerNote, ProductTitleSnapshot = product.Title, PriceSnapshot = product.Price, ProductVersionSnapshot = product.RowVersion.ToArray(),
            CreatedAt = now, ScheduledAt = scheduled, ResponseDeadline = scheduled < now.AddHours(2) ? scheduled : now.AddHours(2) };
        db.PickupReservations.Add(row); await db.SaveChangesAsync();
        PickupNotifications.State(db, row, now); await db.SaveChangesAsync(); await tx.CommitAsync();
        return new(await Map(row), false);
    }
    public async Task<PickupReservationDto> DetailAsync(int actorId, int id)
    {
        await Actor(actorId); var row = await Read(actorId, id);
        await CleanupProduct(row.ProductId);
        return await Map(await Read(actorId, id));
    }
    public async Task<PickupReservationPage> ListAsync(int actorId, PickupReservationQuery query, bool shop)
    {
        Validate(query); var actor = await Actor(actorId);
        if (query.Page > 1000000) throw Error(400, "INVALID_REQUEST", "Trang vượt giới hạn.");
        if (shop && actor.Role != "Shopper") throw Error(403, "ROLE_NOT_ALLOWED", "Cần tài khoản shop.");
        IQueryable<PickupReservation> Rows() => Visible(actorId).Where(r => shop ? r.SellerId == actorId && db.Stores.Any(s => s.Id == r.StoreId && s.OwnerId == actorId) : r.BuyerId == actorId);
        var now = Now;
        var dueProducts = await Rows().Where(r => (r.Status == "PENDING" && r.ResponseDeadline <= now) || (r.Status == "ACCEPTED" && r.ExpiresAt <= now)).Select(r => r.ProductId).Distinct().OrderBy(id => id).ToListAsync();
        foreach (var productId in dueProducts) await CleanupProduct(productId);
        var rows = Rows(); if (query.Status != null) rows = rows.Where(r => r.Status == query.Status);
        var total = await rows.CountAsync();
        var items = await rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();
        var result = new List<PickupReservationDto>(); foreach (var row in items) result.Add(await Map(row));
        return new() { Items = result, TotalItems = total, Page = query.Page, PageSize = query.PageSize, ServerTime = Utc(Now) };
    }
    public Task<PickupReservationDto> AcceptAsync(int actorId, int id, PickupVersionRequest request) => Change(actorId, id, request, "ACCEPTED", null);
    public Task<PickupReservationDto> RejectAsync(int actorId, int id, ClosePickupReservationRequest request) => Change(actorId, id, request, "REJECTED", request.Reason);
    public Task<PickupReservationDto> CancelAsync(int actorId, int id, ClosePickupReservationRequest request) => Change(actorId, id, request, "CANCELLED", request.Reason);
    private async Task<PickupReservationDto> Change(int actorId, int id, PickupVersionRequest request, string next, string? reason)
    {
        Validate(request); var actor = await Actor(actorId); var initial = await Read(actorId, id);
        if (next != "CANCELLED" && (actor.Role != "Shopper" || initial.SellerId != actorId)) throw Error(403, "ROLE_NOT_ALLOWED", "Chỉ shop của món được duyệt hoặc từ chối.");
        await CleanupProduct(initial.ProductId);
        await using var tx = await db.Database.BeginTransactionAsync();
        var product = await LockProduct(initial.ProductId);
        var row = await db.PickupReservations.SingleAsync(r => r.Id == id);
        actor = await Actor(actorId);
        var shop = await StoreFor(product);
        var isSeller = actor.Role == "Shopper" && row.SellerId == actorId && shop.OwnerId == actorId;
        if ((next != "CANCELLED" && !isSeller) || (next == "CANCELLED" && row.BuyerId != actorId && !isSeller)) throw Missing();
        var now = Now;
        await PickupReservationLifecycle.ExpireProductAsync(db, product, now);
        if (row.Status == "EXPIRED") { await tx.CommitAsync(); throw Error(409, "RESERVATION_EXPIRED", "Lượt giữ đã hết hạn."); }
        reason = reason?.Trim();
        if (row.Status == next && ((next == "ACCEPTED" && product.ReservedPickupReservationId == row.Id) ||
            (row.ClosedById == actorId && row.CloseReason == reason)))
        {
            await tx.CommitAsync(); return await Map(row);
        }
        Version(row, request.Version);
        if (row.Status != "PENDING" && !(next == "CANCELLED" && row.Status == "ACCEPTED")) throw Error(409, "INVALID_STATE", "Lượt giữ không còn ở trạng thái phù hợp.");
        if (next == "ACCEPTED")
        {
            Eligible(shop, product); Available(product);
            var buyerRole = await db.Users.Where(u => u.Id == row.BuyerId).Select(u => u.Role).SingleAsync();
            if (buyerRole is not ("CasualUser" or "Shopper") || shop.OwnerId != row.SellerId) throw Error(403, "STORE_NOT_ELIGIBLE", "Người mua hoặc chủ shop không còn hợp lệ.");
            if (!product.RowVersion.SequenceEqual(row.ProductVersionSnapshot)) throw Error(409, "PRODUCT_CHANGED", "Sản phẩm đã đổi. Khách cần xem lại và tạo yêu cầu mới.");
            var location = Location(shop);
            if (string.IsNullOrWhiteSpace(location.Address)) throw Error(403, "STORE_NOT_ELIGIBLE", "Shop cần có địa chỉ nhận hàng.");
            var secret = codes.Issue(row.Id);
            now = Now;
            if (now >= row.ResponseDeadline) { await PickupReservationLifecycle.ExpireProductAsync(db, product, now); await tx.CommitAsync(); throw Error(409, "RESERVATION_EXPIRED", "Đã quá hạn phản hồi."); }
            row.Status = "ACCEPTED"; row.AcceptedAt = now; row.ExpiresAt = row.ScheduledAt.AddMinutes(30);
            row.StoreNameSnapshot = shop.Name; row.StoreAddressSnapshot = location.Address; row.StoreLatitudeSnapshot = location.Lat; row.StoreLongitudeSnapshot = location.Lng;
            row.PickupCodeHash = secret.Hash; row.ProtectedPickupCode = secret.Protected; row.UpdatedAt = now;
            product.Status = "LOCKED"; product.ReservedPickupReservationId = row.Id; product.UpdatedAt = now;
            var others = await db.PickupReservations.Where(r => r.ProductId == product.Id && r.Id != row.Id && r.Status == "PENDING").ToListAsync();
            foreach (var other in others) { Close(other, "CANCELLED", actorId, "PRODUCT_RESERVED_ELSEWHERE", now); PickupNotifications.State(db, other, now); }
        }
        else
        {
            Close(row, next, actorId, reason!, now);
            PickupReservationLifecycle.Release(product, row.Id, now);
        }
        PickupNotifications.State(db, row, now);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return await Map(row);
    }
    private static void Close(PickupReservation row, string status, int actor, string reason, DateTime now)
    { row.Status = status; row.ClosedById = actor; row.CloseReason = reason; row.ClosedAt = row.UpdatedAt = now; }
    private static (string? Address, double? Lat, double? Lng) Location(Store shop)
    {
        var legacy = shop.Locations.OrderBy(l => l.Id).FirstOrDefault();
        var address = string.IsNullOrWhiteSpace(shop.Address) ? legacy?.Address : shop.Address;
        var lat = shop.Latitude ?? legacy?.Latitude; var lng = shop.Longitude ?? legacy?.Longitude;
        if (lat == null || lng == null || !double.IsFinite(lat.Value) || !double.IsFinite(lng.Value) || lat is < -90 or > 90 || lng is < -180 or > 180) return (address, null, null);
        return (address, lat, lng);
    }
    private async Task<PickupReservationDto> Map(PickupReservation row)
    {
        var shop = await db.Stores.AsNoTracking().Include(s => s.Locations).SingleAsync(s => s.Id == row.StoreId);
        var location = Location(shop);
        return new() { Id = row.Id, BuyerId = row.BuyerId, SellerId = row.SellerId, StoreId = row.StoreId, ProductId = row.ProductId,
            Status = row.Status, ContactName = row.ContactName, ContactPhone = row.ContactPhone, BuyerNote = row.BuyerNote,
            ProductTitle = row.ProductTitleSnapshot, Price = row.PriceSnapshot, StoreName = row.StoreNameSnapshot, StoreAddress = row.StoreAddressSnapshot,
            StoreLatitude = row.StoreLatitudeSnapshot, StoreLongitude = row.StoreLongitudeSnapshot,
            StoreLocationChanged = row.AcceptedAt != null && (row.StoreAddressSnapshot != location.Address || row.StoreLatitudeSnapshot != location.Lat || row.StoreLongitudeSnapshot != location.Lng),
            ScheduledAt = Utc(row.ScheduledAt), ResponseDeadline = Utc(row.ResponseDeadline), CreatedAt = Utc(row.CreatedAt),
            AcceptedAt = MaybeUtc(row.AcceptedAt), ExpiresAt = MaybeUtc(row.ExpiresAt), ClosedAt = MaybeUtc(row.ClosedAt), UpdatedAt = MaybeUtc(row.UpdatedAt),
            CloseReason = row.CloseReason, Version = Convert.ToBase64String(row.RowVersion), ServerTime = Utc(Now) };
    }
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static DateTimeOffset? MaybeUtc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
}
