using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Order
{
    [Table("PaymentLogs")]
    public class PaymentLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Gateway { get; set; } = "PayOS"; // PayOS, SePay

        [Required]
        [MaxLength(100)]
        public string TransactionCode { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public string? RawPayload { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
