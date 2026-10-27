using System.Data;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Doosii.BLL.Services;

public sealed class CommunityMediaCleanupService(AppDbContext db, ICommunityMediaStorage storage, ILogger<CommunityMediaCleanupService> logger)
{
    public async Task<int> ProcessAsync(CancellationToken token = default)
    {
        var ids = await db.CommunityMediaCleanup.AsNoTracking().Where(q => q.DeleteAfter <= DateTime.UtcNow).OrderBy(q => q.DeleteAfter).Select(q => q.PublicId).Take(20).ToListAsync(token);
        int deleted = 0;
        foreach (var id in ids)
        {
            token.ThrowIfCancellationRequested();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var pending = await db.CommunityMediaCleanup.FromSqlInterpolated($"SELECT * FROM [forum].[CommunityMediaCleanup] WITH (UPDLOCK,HOLDLOCK) WHERE [PublicId]={id}").SingleOrDefaultAsync(token);
            if (pending == null || pending.DeleteAfter > DateTime.UtcNow) { await tx.CommitAsync(token); continue; }
            if (await db.PostMedia.AnyAsync(m => m.StoragePublicId == id, token))
            { db.CommunityMediaCleanup.Remove(pending); await db.SaveChangesAsync(token); await tx.CommitAsync(token); continue; }
            try { await storage.DeleteAsync(id, pending.MediaType); db.CommunityMediaCleanup.Remove(pending); deleted++; }
            catch (Exception ex)
            {
                pending.Attempts++; pending.DeleteAfter = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(pending.Attempts, 6))));
                logger.LogWarning(ex, "Community media cleanup deferred for {PublicId}", id);
            }
            await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        }
        return deleted;
    }
}
