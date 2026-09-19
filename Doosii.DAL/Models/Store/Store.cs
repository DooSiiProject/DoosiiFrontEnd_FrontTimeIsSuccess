using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Doosii.DAL.Models;

namespace Doosii.DAL.Models.Store
{
    [Table("Stores", Schema = "store")]
    public class Store
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int OwnerId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string? OpeningHours { get; set; }

        public bool IsActive { get; set; } = true;

        [Column(TypeName = "decimal(3,2)")]
        public decimal RatingAverage { get; set; } = 0;

        public int RatingCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation
        [ForeignKey(nameof(OwnerId))]
        public User Owner { get; set; } = null!;

        public ICollection<Product> Products { get; set; } = new List<Product>();

        public ICollection<StoreLocation> Locations { get; set; } = new List<StoreLocation>();
    }
}