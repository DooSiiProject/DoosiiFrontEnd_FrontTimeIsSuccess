using Microsoft.EntityFrameworkCore;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Store;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Notification;
using Doosii.DAL.Models.Forum;

namespace Doosii.DAL.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<EmailOtp> EmailOtps { get; set; }
        public DbSet<MerchantProfile> MerchantProfiles { get; set; }

        // Store schema (Track 1 - Tri)
        public DbSet<Store> Stores { get; set; }
        public DbSet<StoreLocation> StoreLocations { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<BaleAnnouncement> BaleAnnouncements { get; set; }
        public DbSet<StoreReview> StoreReviews { get; set; }

        // Forum schema (Track 1 - Tri)
        public DbSet<PassPost> PassPosts { get; set; }
        public DbSet<PostImage> PostImages { get; set; }
        public DbSet<SpotSharingPost> SpotSharingPosts { get; set; }
        public DbSet<SpotImage> SpotImages { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Reaction> Reactions { get; set; }
        public DbSet<PostReport> PostReports { get; set; }

        // Order & Escrow Module (Track 2 - Wee)
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<EscrowTransaction> EscrowTransactions { get; set; }
        public DbSet<PaymentLog> PaymentLogs { get; set; }
        public DbSet<Dispute> Disputes { get; set; }
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }

        // Notification Module (Track 2 - Wee)
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users", "auth");
                entity.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<MerchantProfile>(entity =>
            {
                entity.ToTable("MerchantProfiles", "auth");

                entity.HasOne(m => m.User)
                      .WithOne(u => u.MerchantProfile)
                      .HasForeignKey<MerchantProfile>(m => m.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(m => m.UserId).IsUnique();

                entity.Property(m => m.KycStatus)
                      .HasMaxLength(20)
                      .HasDefaultValue("PENDING");
            });

            // Store schema configurations
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Slug).IsUnique();
            });

            modelBuilder.Entity<Store>(entity =>
            {
                entity.HasOne(s => s.Owner)
                      .WithMany(u => u.Stores)
                      .HasForeignKey(s => s.OwnerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasOne(p => p.Store)
                      .WithMany(s => s.Products)
                      .HasForeignKey(p => p.StoreId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(p => p.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => p.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<StoreLocation>(entity =>
            {
                entity.HasOne(l => l.Store)
                      .WithMany(s => s.Locations)
                      .HasForeignKey(l => l.StoreId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProductImage>(entity =>
            {
                entity.HasOne(i => i.Product)
                      .WithMany(p => p.Images)
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // RefreshToken & OTP configurations
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(r => r.Token)
                      .IsUnique();

                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EmailOtp>(entity =>
            {
                entity.HasIndex(o => new { o.Email, o.OtpCode, o.Purpose });
            });

            // Order Relationships & Constraints
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasOne(o => o.Buyer)
                      .WithMany()
                      .HasForeignKey(o => o.BuyerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(o => o.Seller)
                      .WithMany()
                      .HasForeignKey(o => o.SellerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(o => o.Status);
                entity.HasIndex(o => o.CreatedAt);
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasOne(oi => oi.Order)
                      .WithMany(o => o.Items)
                      .HasForeignKey(oi => oi.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EscrowTransaction>(entity =>
            {
                entity.HasOne(et => et.Order)
                      .WithMany(o => o.EscrowTransactions)
                      .HasForeignKey(et => et.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PaymentLog>(entity =>
            {
                entity.HasOne(pl => pl.Order)
                      .WithMany(o => o.PaymentLogs)
                      .HasForeignKey(pl => pl.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(pl => pl.TransactionCode);
            });

            modelBuilder.Entity<Dispute>(entity =>
            {
                entity.HasOne(d => d.Order)
                      .WithOne(o => o.Dispute)
                      .HasForeignKey<Dispute>(d => d.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.RaisedByUser)
                      .WithMany()
                      .HasForeignKey(d => d.RaisedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<WithdrawalRequest>(entity =>
            {
                entity.HasOne(wr => wr.User)
                      .WithMany()
                      .HasForeignKey(wr => wr.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(wr => wr.Status);
            });

            // Notification schema
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications", "notification");

                entity.HasOne(n => n.User)
                      .WithMany()
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(n => n.UserId);
                entity.HasIndex(n => n.CreatedAt);
            });

            // Store schema - BaleAnnouncement
            modelBuilder.Entity<BaleAnnouncement>(entity =>
            {
                entity.ToTable("BaleAnnouncements", "store");

                entity.HasOne(b => b.Store)
                      .WithMany()
                      .HasForeignKey(b => b.StoreId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(b => b.Seller)
                      .WithMany()
                      .HasForeignKey(b => b.SellerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(b => b.StoreId);
                entity.HasIndex(b => b.Status);
                entity.HasIndex(b => b.EventTime);
            });

            // Forum schema - PassPost
            modelBuilder.Entity<PassPost>(entity =>
            {
                entity.ToTable("PassPosts", "forum", t =>
                {
                    t.HasCheckConstraint("CK_PassPosts_Price", "[Price] > 0");
                    t.HasCheckConstraint("CK_PassPosts_ConditionPercent", "[ConditionPercent] >= 0 AND [ConditionPercent] <= 100");
                    t.HasCheckConstraint("CK_PassPosts_Status", "[Status] IN ('ACTIVE','LOCKED','SOLD','HIDDEN')");
                });

                entity.HasOne(p => p.Author)
                      .WithMany()
                      .HasForeignKey(p => p.AuthorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(p => p.Status)
                      .HasDefaultValue("ACTIVE");

                entity.Property(p => p.AllowEscrow)
                      .HasDefaultValue(true);

                entity.HasIndex(p => p.AuthorId);
                entity.HasIndex(p => p.Status);
                entity.HasIndex(p => p.CreatedAt);
            });

            modelBuilder.Entity<PostImage>(entity =>
            {
                entity.ToTable("PostImages", "forum");

                entity.HasOne(i => i.Post)
                      .WithMany(p => p.Images)
                      .HasForeignKey(i => i.PostId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(i => new { i.PostId, i.DisplayOrder });
            });

            // Forum schema - SpotSharingPost (fully configured)
            modelBuilder.Entity<SpotSharingPost>(entity =>
            {
                entity.ToTable("SpotSharingPosts", "forum", t =>
                {
                    t.HasCheckConstraint("CK_SpotSharingPosts_Status", "[Status] IN ('ACTIVE','HIDDEN')");
                });

                entity.HasOne(s => s.Author)
                      .WithMany()
                      .HasForeignKey(s => s.AuthorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.TaggedStore)
                      .WithMany()
                      .HasForeignKey(s => s.TaggedStoreId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(s => s.Status)
                      .HasDefaultValue("ACTIVE");

                entity.HasIndex(s => s.AuthorId);
                entity.HasIndex(s => s.TaggedStoreId);
                entity.HasIndex(s => s.CreatedAt);
                entity.HasIndex(s => s.Latitude);
                entity.HasIndex(s => s.Longitude);
            });

            // Forum schema - SpotImage
            modelBuilder.Entity<SpotImage>(entity =>
            {
                entity.ToTable("SpotImages", "forum");

                entity.HasOne(i => i.SpotPost)
                      .WithMany(p => p.Images)
                      .HasForeignKey(i => i.SpotPostId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(i => new { i.SpotPostId, i.DisplayOrder });
            });

            // Forum schema - Comment (placeholder)
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.ToTable("Comments", "forum");

                entity.HasOne(c => c.Author)
                      .WithMany()
                      .HasForeignKey(c => c.AuthorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.AuthorId);
                entity.HasIndex(c => new { c.PostType, c.PostId });
                entity.HasIndex(c => c.CreatedAt);
            });

            // Forum schema - Reaction (placeholder)
            modelBuilder.Entity<Reaction>(entity =>
            {
                entity.ToTable("Reactions", "forum");

                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(r => new { r.UserId, r.PostType, r.PostId })
                      .IsUnique();
            });

            // Forum schema - PostReport (placeholder)
            modelBuilder.Entity<PostReport>(entity =>
            {
                entity.ToTable("PostReports", "forum");

                entity.HasOne(pr => pr.ReportedByUser)
                      .WithMany()
                      .HasForeignKey(pr => pr.ReportedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(pr => pr.Status);
                entity.HasIndex(pr => new { pr.PostType, pr.PostId });
            });

            // Store schema - StoreReview
            modelBuilder.Entity<StoreReview>(entity =>
            {
                entity.ToTable("StoreReviews", "store", t =>
                {
                    t.HasCheckConstraint("CK_StoreReviews_Rating", "[Rating] >= 1 AND [Rating] <= 5");
                });

                entity.HasOne(r => r.Store)
                      .WithMany(s => s.Reviews)
                      .HasForeignKey(r => r.StoreId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.User)
                      .WithMany(u => u.StoreReviews)
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Order)
                      .WithMany()
                      .HasForeignKey(r => r.OrderId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(r => new { r.StoreId, r.UserId })
                      .IsUnique();
            });
        }
    }
}

