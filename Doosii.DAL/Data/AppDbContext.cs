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

        public DbSet<UploadedImage> UploadedImages { get; set; }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<EmailOtp> EmailOtps { get; set; }
        public DbSet<MerchantProfile> MerchantProfiles { get; set; }

        // Store schema (Track 1 - Tri)
        public DbSet<Store> Stores { get; set; }
        public DbSet<ShopRelocationRequest> ShopRelocationRequests { get; set; }
        public DbSet<StoreLocation> StoreLocations { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductGroup> ProductGroups { get; set; }
        public DbSet<ProductGroupImage> ProductGroupImages { get; set; }
        public DbSet<ProductGroupMutation> ProductGroupMutations { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<BaleAnnouncement> BaleAnnouncements { get; set; }
        public DbSet<StoreReview> StoreReviews { get; set; }

        public DbSet<Post> Posts { get; set; }
        public DbSet<CommunityMediaCleanup> CommunityMediaCleanup { get; set; }
        public DbSet<PassPostInfo> PassPostInfos { get; set; }
        public DbSet<PostMedia> PostMedia { get; set; }

        // Legacy CLR accessors retained until B3/B4; excluded from EF mapping.
        public DbSet<PassPost> PassPosts { get; set; }
        public DbSet<PostImage> PostImages { get; set; }
        public DbSet<SpotSharingPost> SpotSharingPosts { get; set; }
        public DbSet<SpotImage> SpotImages { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Reaction> Reactions { get; set; }
        public DbSet<PostReport> PostReports { get; set; }

        // Chat Module (Track 1 - Tri)
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        // Order & Escrow Module (Track 2 - Wee)
        public DbSet<Order> Orders { get; set; }
        public DbSet<PickupReservation> PickupReservations { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<EscrowTransaction> EscrowTransactions { get; set; }
        public DbSet<PaymentLog> PaymentLogs { get; set; }
        public DbSet<Dispute> Disputes { get; set; }
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }

        // Notification Module (Track 2 - Wee)
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PickupNotificationOutbox> PickupNotificationOutbox { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            PickupModelConfiguration.Configure(modelBuilder);
            modelBuilder.Entity<UploadedImage>(e => {
                e.HasIndex(i => i.PublicId).IsUnique();
                e.HasIndex(i => new { i.OwnerId, i.CreatedAt });
                e.HasOne<User>().WithMany().HasForeignKey(i => i.OwnerId).OnDelete(DeleteBehavior.Restrict);
                e.ToTable("UploadedImages", "media", t => t.HasCheckConstraint("CK_UploadedImages_State", "[State] IN ('ACTIVE','DELETING','DELETED')"));
            });
            modelBuilder.Entity<ProductGroup>(e =>
            {
                e.HasAlternateKey(g => new { g.Id, g.StoreId });
                e.HasOne(g => g.Store).WithMany().HasForeignKey(g => g.StoreId).OnDelete(DeleteBehavior.Restrict);
                e.Property(g => g.IsPublished).HasDefaultValue(true);
                e.HasIndex(g => new { g.StoreId, g.CreatedAt, g.Id });
            });
            modelBuilder.Entity<ProductGroupImage>(e =>
            {
                e.HasOne(i => i.ProductGroup).WithMany(g => g.Images).HasForeignKey(i => i.ProductGroupId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(i => new { i.ProductGroupId, i.DisplayOrder, i.Id });
                e.ToTable("ProductGroupImages", "store", t => t.HasCheckConstraint("CK_ProductGroupImages_Position", "[DisplayOrder] >= 0"));
            });
            modelBuilder.Entity<ProductGroupMutation>(e =>
            {
                e.HasOne(m => m.ProductGroup).WithMany().HasForeignKey(m => new { m.ProductGroupId, m.StoreId })
                    .HasPrincipalKey(g => new { g.Id, g.StoreId }).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(m => new { m.StoreId, m.ClientRequestId }).IsUnique();
                e.ToTable("ProductGroupMutations", "store", t => t.HasCheckConstraint("CK_ProductGroupMutations_Operation", "[Operation] IN (N'CREATE', N'UPDATE')"));
            });
            modelBuilder.Entity<Product>(e =>
            {
                e.HasOne(p => p.ProductGroup).WithMany(g => g.Products).HasForeignKey(p => new { p.ProductGroupId, p.StoreId })
                    .HasPrincipalKey(g => new { g.Id, g.StoreId }).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(p => new { p.ProductGroupId, p.GroupPosition, p.Id });
                e.ToTable("Products", "store", t => t.HasCheckConstraint("CK_Products_GroupPosition",
                    "([ProductGroupId] IS NULL AND [GroupPosition] IS NULL) OR ([ProductGroupId] IS NOT NULL AND [GroupPosition] IS NOT NULL AND [GroupPosition] >= 0)"));
            });
            modelBuilder.Entity<ShopRelocationRequest>(entity =>
            {
                entity.HasOne(r => r.Store).WithMany().HasForeignKey(r => r.StoreId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(r => r.StoreId).IsUnique().HasFilter("[Status] = N'PENDING'").HasDatabaseName("IX_ShopRelocationRequests_OnePendingPerStore");
                entity.HasIndex(r => new { r.StoreId, r.ClientRequestId }).IsUnique();
                entity.HasIndex(r => r.ReviewRequestId).IsUnique().HasFilter("[ReviewRequestId] IS NOT NULL");
                entity.ToTable("ShopRelocationRequests", "store", table =>
                {
                    table.HasCheckConstraint("CK_ShopRelocationRequests_Status", "[Status] IN (N'PENDING', N'CANCELLED', N'APPROVED', N'REJECTED')");
                    table.HasCheckConstraint("CK_ShopRelocationRequests_Coordinates", "[ProposedLatitude] >= -90 AND [ProposedLatitude] <= 90 AND [ProposedLongitude] >= -180 AND [ProposedLongitude] <= 180");
                });
            });
            modelBuilder.Entity<BaleAnnouncement>().HasIndex(a => new { a.SellerId, a.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
            modelBuilder.Entity<Product>().HasIndex(p => p.StoreId);
            modelBuilder.Entity<Product>().Property(p => p.IsPublished).HasDefaultValue(true);
            modelBuilder.Entity<Product>().HasIndex(p => new { p.StoreId, p.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");

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

                entity.HasIndex(o => o.BuyerId);
                entity.HasIndex(o => new { o.BuyerId, o.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
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
                entity.HasIndex(pl => new { pl.Gateway, pl.TransactionCode }).IsUnique();
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
                entity.HasIndex(wr => new { wr.UserId, wr.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
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

            CommunityModelConfiguration.Configure(modelBuilder);

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

            // Forum schema - Conversation (1-1 Chat)
            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.ToTable("Conversations", "forum");

                entity.HasOne(c => c.Buyer)
                      .WithMany()
                      .HasForeignKey(c => c.BuyerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Seller)
                      .WithMany()
                      .HasForeignKey(c => c.SellerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Product)
                      .WithMany()
                      .HasForeignKey(c => c.ProductId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(c => c.BuyerId);
                entity.HasIndex(c => c.SellerId);
                entity.HasIndex(c => c.ProductId);
                entity.HasIndex(c => c.LastMessageAt);

                // Unique: mot cap buyer-seller-product chi co 1 conversation
                entity.HasIndex(c => new { c.BuyerId, c.SellerId, c.ProductId })
                      .IsUnique();

                entity.Property(c => c.IsClosed).HasDefaultValue(false);
                entity.Property(c => c.LastMessageAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // Forum schema - ChatMessage
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.ToTable("ChatMessages", "forum", t =>
                {
                    t.HasCheckConstraint("CK_ChatMessages_MessageType",
                        "[MessageType] IN ('TEXT','PRODUCT_CARD','SYSTEM')");
                });

                entity.HasOne(m => m.Conversation)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.Sender)
                      .WithMany()
                      .HasForeignKey(m => m.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Product)
                      .WithMany()
                      .HasForeignKey(m => m.ProductId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(m => new { m.ConversationId, m.SenderId, m.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
                entity.HasIndex(m => m.ConversationId);
                entity.HasIndex(m => m.SentAt);

                entity.Property(m => m.MessageType).HasDefaultValue("TEXT");
                entity.Property(m => m.IsRead).HasDefaultValue(false);
                entity.Property(m => m.IsDeleted).HasDefaultValue(false);
            });

        }
    }
}
