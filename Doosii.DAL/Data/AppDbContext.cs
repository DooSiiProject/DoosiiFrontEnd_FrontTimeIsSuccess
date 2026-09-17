using Microsoft.EntityFrameworkCore;
using Doosii.DAL.Models;

namespace Doosii.DAL.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<MerchantProfile> MerchantProfiles { get; set; }

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
        }
    }
}
