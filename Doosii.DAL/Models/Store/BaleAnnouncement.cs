using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Store
{
    public static class AnnouncementStatus
    {
        public const string PendingPayment = "PENDING_PAYMENT";
        public const string PaidActive = "PAID_ACTIVE";
        public const string Cancelled = "CANCELLED";
    }

    [Table("BaleAnnouncements", Schema = "store")]
    public class BaleAnnouncement
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int StoreId { get; set; }

        [ForeignKey(nameof(StoreId))]
        public Store Store { get; set; } = null!;

        [Required]
        public int SellerId { get; set; }

        [ForeignKey(nameof(SellerId))]
        public User Seller { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? BannerUrl { get; set; }

        [Required]
        public DateTime EventTime { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BroadcastFee { get; set; } = 50000;

        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "WALLET";

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = AnnouncementStatus.PendingPayment;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
