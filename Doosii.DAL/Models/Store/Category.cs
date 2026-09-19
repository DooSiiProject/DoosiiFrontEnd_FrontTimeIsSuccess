using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Store
{
    [Table("Categories", Schema = "store")]
    public class Category
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Slug { get; set; } = string.Empty;

        // Navigation
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}