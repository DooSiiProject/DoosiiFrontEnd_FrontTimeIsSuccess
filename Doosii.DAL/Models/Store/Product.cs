using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Store
{
    [Table("Products", Schema = "store")]
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int StoreId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [MaxLength(50)]
        public string? Size { get; set; }

        /// <summary>
        /// Tinh trang san pham tu 0 den 100.
        /// </summary>
        [Range(0, 100)]
        public int ConditionPercent { get; set; } = 100;

        /// <summary>
        /// Trang thai: AVAILABLE | LOCKED | SOLD
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "AVAILABLE";

        /// <summary>
        /// Luu danh sach tag dang JSON hoac comma-separated.
        /// </summary>
        [MaxLength(500)]
        public string? StyleTags { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation
        [ForeignKey(nameof(StoreId))]
        public Store Store { get; set; } = null!;

        [ForeignKey(nameof(CategoryId))]
        public Category Category { get; set; } = null!;

        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}