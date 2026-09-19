using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Store
{
    [Table("ProductImages", Schema = "store")]
    public class ProductImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [MaxLength(1000)]
        public string ImageUrl { get; set; } = string.Empty;

        public int DisplayOrder { get; set; } = 0;

        // Navigation
        [ForeignKey(nameof(ProductId))]
        public Product Product { get; set; } = null!;
    }
}