using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Doosii.DAL.Data;

public static class CategoryReferenceData
{
    public static async Task EnsureAsync(AppDbContext context)
    {
        var categories = new[] {
            ("top", "Áo"), ("bottom", "Quần / chân váy"), ("outerwear", "Áo khoác"),
            ("full-body", "Đầm / đồ liền thân"), ("footwear", "Giày dép"), ("accessory", "Phụ kiện")
        };
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (var (slug, name) in categories)
        {
            // Range locks on unique Slug serialize simultaneous app startups.
            // Existing labels and IDs are preserved, and no sample inventory is inserted.
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [store].[Categories] ([Name], [Slug]) SELECT {name}, {slug} WHERE NOT EXISTS (SELECT 1 FROM [store].[Categories] WITH (UPDLOCK, HOLDLOCK) WHERE [Slug] = {slug})");
        }
        await transaction.CommitAsync();
    }
}
