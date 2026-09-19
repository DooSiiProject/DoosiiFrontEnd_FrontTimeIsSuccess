using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Store
{
    [Table("StoreLocations", Schema = "store")]
    public class StoreLocation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int StoreId { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        // Navigation
        [ForeignKey(nameof(StoreId))]
        public Store Store { get; set; } = null!;
    }
}