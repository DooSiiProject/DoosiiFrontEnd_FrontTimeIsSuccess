using Doosii.DAL.Models.Forum;
using Doosii.DAL.Models.Store;
using Microsoft.EntityFrameworkCore;

namespace Doosii.DAL.Data;

internal static class CommunityModelConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommunityMediaCleanup>(e =>
        {
            e.HasIndex(m => m.DeleteAfter);
            e.ToTable("CommunityMediaCleanup", "forum", t => t.HasCheckConstraint("CK_CommunityMediaCleanup_Type", "[MediaType] IN ('IMAGE','VIDEO')"));
        });
        // Retained CLR types only allow old services to compile until B3/B4.
        // Legacy HTTP endpoints are gated; none of these types has a table mapping.
        modelBuilder.Ignore<PassPost>();
        modelBuilder.Ignore<PostImage>();
        modelBuilder.Ignore<SpotSharingPost>();
        modelBuilder.Ignore<SpotImage>();

        modelBuilder.Entity<Post>(e =>
        {
            e.HasAlternateKey(p => new { p.Id, p.Type });
            e.HasOne(p => p.Author).WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => new { p.AuthorId, p.ClientRequestId }).IsUnique();
            e.HasIndex(p => new { p.VisibilityStatus, p.CreatedAt, p.Id });
            e.HasIndex(p => new { p.Type, p.VisibilityStatus, p.CreatedAt, p.Id });
            e.HasIndex(p => new { p.AuthorId, p.CreatedAt });
            e.Property(p => p.VisibilityStatus).HasDefaultValue("ACTIVE");
            e.ToTable("Posts", "forum", t =>
            {
                t.HasCheckConstraint("CK_Posts_Type", "[Type] IN ('PASS','OTHER')");
                t.HasCheckConstraint("CK_Posts_Visibility", "[VisibilityStatus] IN ('ACTIVE','HIDDEN')");
                t.HasCheckConstraint("CK_Posts_Text", "LEN(LTRIM(RTRIM([Title]))) > 0 AND LEN(LTRIM(RTRIM([PostDescription]))) > 0");
                t.HasCheckConstraint("CK_Posts_RequestId", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000'");
            });
        });
        modelBuilder.Entity<PassPostInfo>(e =>
        {
            e.Property(p => p.PostId).ValueGeneratedNever();
            e.HasOne(p => p.Post).WithOne(p => p.PassInfo)
                .HasForeignKey<PassPostInfo>(p => new { p.PostId, p.PostType })
                .HasPrincipalKey<Post>(p => new { p.Id, p.Type }).OnDelete(DeleteBehavior.Restrict);
            e.Property(p => p.SaleStatus).HasDefaultValue("AVAILABLE");
            e.ToTable("PassPostInfos", "forum", t =>
            {
                t.HasCheckConstraint("CK_PassPostInfos_Type", "[PostType] = 'PASS'");
                t.HasCheckConstraint("CK_PassPostInfos_Price", "[Price] > 0");
                t.HasCheckConstraint("CK_PassPostInfos_Condition", "[ConditionPercent] BETWEEN 0 AND 100");
                t.HasCheckConstraint("CK_PassPostInfos_SaleStatus", "[SaleStatus] IN ('AVAILABLE','SOLD')");
                t.HasCheckConstraint("CK_PassPostInfos_Text", "LEN(LTRIM(RTRIM([ProductName]))) > 0 AND LEN(LTRIM(RTRIM([Size]))) > 0 AND LEN(LTRIM(RTRIM([Defects]))) > 0");
                t.HasCheckConstraint("CK_PassPostInfos_Tags", "ISJSON([StyleTagsJson]) = 1 AND LEFT(LTRIM([StyleTagsJson]), 1) = '['");
                t.HasCheckConstraint("CK_PassPostInfos_Measurements", "[MeasurementsJson] IS NULL OR (ISJSON([MeasurementsJson]) = 1 AND LEFT(LTRIM([MeasurementsJson]), 1) = '[')");
            });
        });
        // Composite FK prevents tagging a product with a different store.
        modelBuilder.Entity<Product>().HasAlternateKey(p => new { p.Id, p.StoreId });
        modelBuilder.Entity<PostMedia>(e =>
        {
            e.HasOne(m => m.Post).WithMany(p => p.Media).HasForeignKey(m => new { m.PostId, m.PostType })
                .HasPrincipalKey(p => new { p.Id, p.Type }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Store).WithMany().HasForeignKey(m => m.StoreId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Product).WithMany().HasForeignKey(m => new { m.ProductId, m.StoreId })
                .HasPrincipalKey(p => new { p.Id, p.StoreId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.PostId, m.DisplayOrder }).IsUnique();
            e.HasIndex(m => new { m.StoreId, m.PostId });
            e.ToTable("PostMedia", "forum", t =>
            {
                t.HasCheckConstraint("CK_PostMedia_Type", "[MediaType] IN ('IMAGE','VIDEO')");
                t.HasCheckConstraint("CK_PostMedia_Position", "[DisplayOrder] >= 0");
                t.HasCheckConstraint("CK_PostMedia_PassTags", "[PostType] <> 'PASS' OR ([StoreId] IS NULL AND [ProductId] IS NULL)");
                t.HasCheckConstraint("CK_PostMedia_ProductStore", "[ProductId] IS NULL OR [StoreId] IS NOT NULL");
                t.HasCheckConstraint("CK_PostMedia_Source", "LEN(LTRIM(RTRIM([SourceUrl]))) > 0 AND LEN(LTRIM(RTRIM([StoragePublicId]))) > 0");
            });
        });
        modelBuilder.Entity<Comment>(e =>
        {
            e.Ignore(c => c.PostType);
            e.HasOne<Post>().WithMany().HasForeignKey(c => c.PostId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasAlternateKey(c => new { c.Id, c.PostId });
            e.HasOne(c => c.ParentComment).WithMany(c => c.Replies)
                .HasForeignKey(c => new { c.ParentCommentId, c.PostId })
                .HasPrincipalKey(c => new { c.Id, c.PostId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => new { c.PostId, c.AuthorId, c.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
            e.HasIndex(c => new { c.PostId, c.ParentCommentId, c.CreatedAt, c.Id });
            e.Property(c => c.IsDeleted).HasDefaultValue(false);
            e.ToTable("Comments", "forum", t => t.HasCheckConstraint("CK_Comments_Content", "LEN(LTRIM(RTRIM([Content]))) > 0"));
        });
        modelBuilder.Entity<Reaction>(e =>
        {
            e.Ignore(r => r.PostType);
            e.HasOne<Post>().WithMany().HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.PostId, r.UserId }).IsUnique();
            e.ToTable("Reactions", "forum", t => t.HasCheckConstraint("CK_Reactions_ReactionType", "[ReactionType] IN ('LIKE','HEART')"));
        });
        modelBuilder.Entity<PostReport>(e =>
        {
            e.Ignore(r => r.PostType);
            e.HasOne<Post>().WithMany().HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Reporter).WithMany().HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.ReviewedByAdmin).WithMany().HasForeignKey(r => r.ReviewedByAdminId).OnDelete(DeleteBehavior.Restrict);
            e.Property(r => r.Status).HasDefaultValue("PENDING");
            e.HasIndex(r => new { r.PostId, r.ReporterId }).IsUnique().HasFilter("[Status] = 'PENDING'");
            e.HasIndex(r => new { r.Status, r.CreatedAt, r.Id });
            e.ToTable("PostReports", "forum", t =>
            {
                t.HasCheckConstraint("CK_PostReports_Reason", "[Reason] IN ('SCAM','COUNTERFEIT','OFFENSIVE','PROHIBITED_CONTENT','OTHER')");
                t.HasCheckConstraint("CK_PostReports_Status", "[Status] IN ('PENDING','REVIEWED','DISMISSED','ACTION_TAKEN')");
            });
        });
    }
}
