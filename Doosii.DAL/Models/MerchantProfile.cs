using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models
{
    [Table("MerchantProfiles", Schema = "auth")]
    public class MerchantProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(200)]
        public string StoreName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [Required]
        [MaxLength(20)]
        public string KycStatus { get; set; } = "PENDING";

        [Required]
        [MaxLength(1000)]
        public string LicenseImageUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string FrontFacadeUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string IdCardFrontUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string IdCardBackUrl { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;
    }
}
