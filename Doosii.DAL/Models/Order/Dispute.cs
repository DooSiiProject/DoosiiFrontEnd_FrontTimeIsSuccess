using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Order
{
    public static class DisputeStatus
    {
        public const string PendingReview = "PENDING_REVIEW";
        public const string RefundedBuyer = "REFUNDED_BUYER";
        public const string ReleasedSeller = "RELEASED_SELLER";
    }

    [Table("Disputes")]
    public class Dispute
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; } = null!;

        [Required]
        public int RaisedByUserId { get; set; }

        [ForeignKey(nameof(RaisedByUserId))]
        public User RaisedByUser { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? EvidenceVideoUrl { get; set; }

        public string? EvidenceImagesJson { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = DisputeStatus.PendingReview;

        [MaxLength(1000)]
        public string? AdminVerdict { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }
    }
}
