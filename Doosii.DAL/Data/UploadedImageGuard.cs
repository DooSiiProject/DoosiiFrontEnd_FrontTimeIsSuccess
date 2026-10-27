using Microsoft.EntityFrameworkCore;

namespace Doosii.DAL.Data;

// Share registry row locks with deletion, so a detached image cannot be reattached
// after cloud deletion starts. Keep tombstones for stale forms and delayed retries.
public static class UploadedImageGuard
{
    public static async Task<int> SaveChangesAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        db.ChangeTracker.DetectChanges();
        var values = db.ChangeTracker.Entries().Where(e => e.Entity is not Models.UploadedImage &&
            e.State is EntityState.Added or EntityState.Modified).SelectMany(e => e.Properties
                .Where(p => p.Metadata.ClrType == typeof(string) && (e.State == EntityState.Added || p.IsModified))
                .Select(p => p.CurrentValue as string)).Where(v => v != null && v.Contains("res.cloudinary.com/", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (values.Length == 0) return await db.SaveChangesAsync(cancellationToken);
        var text = string.Join('\n', values).Replace("\\/", "/");
        await using var ownedTransaction = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var images = await db.UploadedImages.FromSqlInterpolated($"SELECT * FROM [media].[UploadedImages] WITH (UPDLOCK,HOLDLOCK) WHERE CHARINDEX([PublicId], {text}) > 0")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (images.Any(i => i.State != "ACTIVE")) throw new InvalidOperationException("Ảnh đã được xóa hoặc đang chờ xóa. Vui lòng chọn ảnh khác.");
        var result = await db.SaveChangesAsync(cancellationToken);
        if (ownedTransaction != null) await ownedTransaction.CommitAsync(cancellationToken);
        return result;
    }
}
