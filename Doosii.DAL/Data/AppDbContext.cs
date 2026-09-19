using Microsoft.EntityFrameworkCore;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Store;

namespace Doosii.DAL.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<MerchantProfile> MerchantProfiles { get; set; }
        
        // Store schema
        public DbSet<Store> Stores { get; set; }
        public DbSet<StoreLocation> StoreLocations { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }

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
        }
    }
}