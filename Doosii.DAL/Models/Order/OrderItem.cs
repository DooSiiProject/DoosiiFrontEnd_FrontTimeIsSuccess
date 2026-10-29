using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Order
{
    [Table("OrderItems")]
    public class OrderItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; } = null!;

        [Required, MaxLength(20)] public string SourceType { get; set; } = "SHOP_PRODUCT";
        public int? ProductId { get; set; }
        public int? PassPostId { get; set; }
        // Historical values: these intentionally do not follow later shop/user edits.
        public int? StoreIdSnapshot { get; set; }
        [MaxLength(256)] public string? StoreNameSnapshot { get; set; }
        [MaxLength(256)] public string? SellerNameSnapshot { get; set; }
        [Required, MaxLength(24)] public string SnapshotOrigin { get; set; } = "AT_ORDER";
        [MaxLength(8)] public byte[]? SourceVersionSnapshot { get; set; }

        // PASS fulfilment is not enabled by a schema migration alone.
        public int RequireShopProductId() => SourceType == "SHOP_PRODUCT" && ProductId.HasValue && PassPostId == null
            ? ProductId.Value : throw new InvalidOperationException("Luồng xử lý đơn PASS chưa được mở.");

        [Required]
        [MaxLength(300)]
        public string ProductNameSnapshot { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceSnapshot { get; set; }

        [MaxLength(1000)]
        public string? ThumbnailSnapshot { get; set; }
    }
}
