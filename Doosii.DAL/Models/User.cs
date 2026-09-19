using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Doosii.DAL.Models.Store;

namespace Doosii.DAL.Models
{
    [Table("Users", Schema = "auth")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        [MaxLength(50)]
        public string Role { get; set; } = "Customer";
        [MaxLength(500)]
        public string? AvatarUrl { get; set; }
        public string? DeliveryAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        // Navigation
        public MerchantProfile? MerchantProfile { get; set; }
        public ICollection<Doosii.DAL.Models.Store.Store> Stores { get; set; } = new List<Doosii.DAL.Models.Store.Store>();
    }
}
