using Microsoft.EntityFrameworkCore;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;

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

        // Order & Escrow Module (Track 2 - Wee)
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<EscrowTransaction> EscrowTransactions { get; set; }
        public DbSet<PaymentLog> PaymentLogs { get; set; }
        public DbSet<Dispute> Disputes { get; set; }
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }

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
        }
    }
}