using Doosii.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Doosii.BLL.Services;

public sealed class PickupMaintenanceService(AppDbContext db, TimeProvider clock, ILogger<PickupMaintenanceService> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.PickupReservations.AsNoTracking().Where(r => (r.Status == "PENDING" || r.Status == "ACCEPTED") &&
            ((r.Status == "PENDING" && r.ResponseDeadline <= now) || (r.Status == "ACCEPTED" && r.ExpiresAt <= now) ||
            !db.Users.Any(u => u.Id == r.BuyerId && (u.Role == "CasualUser" || u.Role == "Shopper")) ||
            !db.Products.Any(p => p.Id == r.ProductId && p.IsPublished && p.Store.IsActive && p.Store.OwnerId == r.SellerId &&
                p.Store.Owner.Role == "Shopper" && p.Store.Owner.MerchantProfile != null && p.Store.Owner.MerchantProfile.KycStatus == "APPROVED")))
            .Select(r => r.ProductId).Distinct().OrderBy(id => id).Take(100).ToListAsync(cancellationToken);
        var processed = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                var product = await db.Products.FromSqlInterpolated($"SELECT * FROM [store].[Products] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleAsync(cancellationToken);
                now = clock.GetUtcNow().UtcDateTime;
                if (await PickupReservationLifecycle.ExpireProductAsync(db, product, now)) processed++;
                processed += await PickupReservationLifecycle.CancelIneligibleProductAsync(db, product, now);
                await tx.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) { logger.LogWarning(error, "Pickup maintenance will retry product {ProductId}", id); }
            finally { db.ChangeTracker.Clear(); }
        }
        return processed;
    }
}
