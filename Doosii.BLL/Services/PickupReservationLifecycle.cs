using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;
using Microsoft.EntityFrameworkCore;

namespace Doosii.BLL.Services;

internal static class PickupReservationLifecycle
{
    // Caller must own the product UPDLOCK in a transaction. Never locks an online order.
    internal static async Task<bool> ExpireProductAsync(AppDbContext db, Product product, DateTime now)
    {
        var rows = await db.PickupReservations.Where(r => r.ProductId == product.Id &&
            ((r.Status == "PENDING" && r.ResponseDeadline <= now) || (r.Status == "ACCEPTED" && r.ExpiresAt <= now))).ToListAsync();
        foreach (var row in rows)
        {
            var wasAccepted = row.Status == "ACCEPTED";
            row.Status = "EXPIRED"; row.ClosedAt = row.UpdatedAt = now; row.ClosedById = null;
            row.CloseReason = wasAccepted ? "PICKUP_TIMEOUT" : "RESPONSE_TIMEOUT";
            if (wasAccepted) Release(product, row.Id, now);
            PickupNotifications.State(db, row, now);
        }
        if (rows.Count > 0) await db.SaveChangesAsync();
        return rows.Count > 0;
    }
    internal static async Task<int> CancelIneligibleProductAsync(AppDbContext db, Product product, DateTime now)
    {
        // Shared store lock coordinates with relocation/eligibility writes; product is locked first.
        var shop = await db.Stores.FromSqlInterpolated($"SELECT * FROM [store].[Stores] WITH (HOLDLOCK) WHERE [Id]={product.StoreId}")
            .AsNoTracking().Include(s => s.Owner).ThenInclude(u => u.MerchantProfile).SingleAsync();
        var rows = await db.PickupReservations.Where(r => r.ProductId == product.Id && (r.Status == "PENDING" || r.Status == "ACCEPTED")).ToListAsync();
        var count = 0;
        foreach (var row in rows)
        {
            var buyerOk = await db.Users.AsNoTracking().AnyAsync(u => u.Id == row.BuyerId && (u.Role == "CasualUser" || u.Role == "Shopper"));
            if (buyerOk && product.IsPublished && shop.IsActive && shop.OwnerId == row.SellerId && shop.Owner.Role == "Shopper" && shop.Owner.MerchantProfile?.KycStatus == "APPROVED") continue;
            row.Status = "CANCELLED"; row.ClosedAt = row.UpdatedAt = now; row.ClosedById = row.SellerId; row.CloseReason = "ELIGIBILITY_LOST";
            Release(product, row.Id, now); PickupNotifications.State(db, row, now); count++;
        }
        if (count > 0) await db.SaveChangesAsync();
        return count;
    }
    internal static void Release(Product product, int reservationId, DateTime now)
    {
        if (product.ReservedPickupReservationId != reservationId) return;
        if (product.Status != "LOCKED" || product.ReservedOrderId != null)
            throw new InvalidOperationException("Pickup reservation lock is inconsistent.");
        product.ReservedPickupReservationId = null; product.Status = "AVAILABLE"; product.UpdatedAt = now;
    }
}
