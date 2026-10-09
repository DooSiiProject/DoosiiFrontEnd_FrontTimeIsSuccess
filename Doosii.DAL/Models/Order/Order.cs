using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Order
{
    public static class OrderStatus
    {
        public const string PendingPayment = "PENDING_PAYMENT";
        public const string EscrowHolding = "ESCROW_HOLDING";
        public const string InTransit = "IN_TRANSIT";
        public const string CompletedReleased = "COMPLETED_RELEASED";
        public const string Disputed = "DISPUTED";
        public const string Refunded = "REFUNDED";
        public const string Cancelled = "CANCELLED";
    }

    [Table("Orders")]
    public class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int BuyerId { get; set; }

        [ForeignKey(nameof(BuyerId))]
        public User Buyer { get; set; } = null!;

        [Required]
        public int SellerId { get; set; }

        [ForeignKey(nameof(SellerId))]
        public User Seller { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProductPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingFee { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal EscrowFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = OrderStatus.PendingPayment;

        [MaxLength(100)]
        public string? ShippingCarrier { get; set; }

        [MaxLength(100)]
        public string? TrackingCode { get; set; }

        [MaxLength(500)]
        public string? PackageImageUrl { get; set; }

        [Required]
        [MaxLength(256)]
        public string ReceiverName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string ReceiverPhone { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string ShippingAddress { get; set; } = string.Empty;

        public DateTime? PaidAt { get; set; }

        public DateTime? ShippedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
        public ICollection<EscrowTransaction> EscrowTransactions { get; set; } = new List<EscrowTransaction>();
        public ICollection<PaymentLog> PaymentLogs { get; set; } = new List<PaymentLog>();
        public Dispute? Dispute { get; set; }
    }
}
