using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.DAL.Models.Order;
using Microsoft.EntityFrameworkCore;

namespace Doosii.BLL.Services;

public sealed partial class PickupReservationService
{
    public async Task<PickupCodeDto> CodeAsync(int actorId, int id)
    {
        await Actor(actorId); var initial = await Read(actorId, id);
        if (initial.BuyerId != actorId) throw Error(403, "ROLE_NOT_ALLOWED", "Chỉ khách giữ đồ được xem mã nhận.");
        await CleanupProduct(initial.ProductId);
        await using var tx = await db.Database.BeginTransactionAsync();
        var product = await LockProduct(initial.ProductId);
        var row = await db.PickupReservations.SingleAsync(r => r.Id == id);
        await Actor(actorId);
        if (row.BuyerId != actorId) throw Missing();
        var shop = await StoreFor(product);
        var now = Now;
        await PickupReservationLifecycle.ExpireProductAsync(db, product, now);
        if (row.Status == "EXPIRED") { await tx.CommitAsync(); throw Error(409, "RESERVATION_EXPIRED", "Lượt giữ đã hết hạn."); }
        if (row.Status != "ACCEPTED") throw Error(409, "INVALID_STATE", "Mã nhận chỉ có khi shop đã duyệt và lượt giữ còn hiệu lực.");
        Eligible(shop, product);
        if (shop.OwnerId != row.SellerId) throw Error(403, "STORE_NOT_ELIGIBLE", "Chủ shop đã thay đổi.");
        EnsurePickupOwner(product, row);
        var code = codes.Read(row.Id, row.PickupCodeHash!, row.ProtectedPickupCode!);
        await tx.CommitAsync();
        return new() { ReservationId = row.Id, PickupCode = code, ExpiresAt = Utc(row.ExpiresAt!.Value), ServerTime = Utc(now) };
    }
    public async Task<PickupReservationDto> CompleteAsync(int actorId, int id, CompletePickupReservationRequest request)
    {
        Validate(request); var actor = await Actor(actorId); var initial = await Read(actorId, id);
        if (actor.Role != "Shopper" || initial.SellerId != actorId) throw Error(403, "ROLE_NOT_ALLOWED", "Chỉ shop của món được xác nhận giao hàng.");
        await CleanupProduct(initial.ProductId);
        await using var tx = await db.Database.BeginTransactionAsync();
        var product = await LockProduct(initial.ProductId);
        var row = await db.PickupReservations.SingleAsync(r => r.Id == id);
        actor = await Actor(actorId);
        var shop = await StoreFor(product);
        if (actor.Role != "Shopper" || row.SellerId != actorId || shop.OwnerId != actorId) throw Missing();
        var now = Now;
        await PickupReservationLifecycle.ExpireProductAsync(db, product, now);
        if (row.Status == "EXPIRED") { await tx.CommitAsync(); throw Error(409, "RESERVATION_EXPIRED", "Lượt giữ đã hết hạn."); }
        if (row.Status == "COMPLETED")
        {
            // A lost response can be retried with the original version/code, with no new side effects.
            if (row.ClosedById != actorId || !codes.Verify(row.Id, request.PickupCode, row.PickupCodeHash!))
                throw Error(409, "INVALID_STATE", "Lượt giữ đã hoàn tất.");
            await tx.CommitAsync(); return await Map(row);
        }
        if (row.Status != "ACCEPTED") throw Error(409, "INVALID_STATE", "Lượt giữ chưa được duyệt hoặc đã kết thúc.");
        Eligible(shop, product);
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == row.BuyerId && (u.Role == "CasualUser" || u.Role == "Shopper")))
            throw Error(403, "STORE_NOT_ELIGIBLE", "Người mua không còn đủ điều kiện.");
        EnsurePickupOwner(product, row);
        var location = Location(shop);
        if (row.StoreAddressSnapshot != location.Address || row.StoreLatitudeSnapshot != location.Lat || row.StoreLongitudeSnapshot != location.Lng)
            throw Error(409, "STORE_LOCATION_CHANGED", "Vị trí shop đã thay đổi. Hãy hủy và tạo lượt giữ tại địa điểm mới.");
        if (row.CodeLockedUntil > now) throw Locked(row, now);
        Version(row, request.Version);
        // Validate the stored material/config before treating a mismatch as a customer's failed attempt.
        _ = codes.Read(row.Id, row.PickupCodeHash!, row.ProtectedPickupCode!);
        if (row.CodeLockedUntil.HasValue) { row.FailedCodeAttempts = 0; row.CodeLockedUntil = null; }
        if (!codes.Verify(row.Id, request.PickupCode, row.PickupCodeHash!))
        {
            row.FailedCodeAttempts++; row.UpdatedAt = now;
            if (row.FailedCodeAttempts >= 5) row.CodeLockedUntil = now.AddMinutes(15);
            await db.SaveChangesAsync(); await tx.CommitAsync();
            if (row.CodeLockedUntil.HasValue) throw Locked(row, now);
            throw new PickupCodeAttemptException(400, "Mã nhận không đúng.", "INVALID_PICKUP_CODE", Convert.ToBase64String(row.RowVersion));
        }
        now = Now;
        if (row.ExpiresAt <= now)
        {
            await PickupReservationLifecycle.ExpireProductAsync(db, product, now); await tx.CommitAsync();
            throw Error(409, "RESERVATION_EXPIRED", "Lượt giữ đã hết hạn.");
        }
        Close(row, "COMPLETED", actorId, "PAYMENT_RECEIVED_AT_STORE", now);
        row.FailedCodeAttempts = 0; row.CodeLockedUntil = null;
        product.Status = "SOLD"; product.ReservedPickupReservationId = null; product.UpdatedAt = now;
        PickupNotifications.State(db, row, now);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return await Map(row);
    }
    private static PickupCodeAttemptException Locked(PickupReservation row, DateTime now) => new(429,
        "Nhập sai mã quá nhiều lần. Vui lòng thử lại sau.", "PICKUP_CODE_LOCKED", Convert.ToBase64String(row.RowVersion),
        Math.Max(1, (int)Math.Ceiling((row.CodeLockedUntil!.Value - now).TotalSeconds)));
    private static void EnsurePickupOwner(Doosii.DAL.Models.Store.Product product, PickupReservation row)
    {
        if (product.Status != "LOCKED" || product.ReservedPickupReservationId != row.Id || product.ReservedOrderId != null)
            throw Error(409, "PRODUCT_UNAVAILABLE", "Sản phẩm không còn thuộc lượt giữ này.");
    }
}
