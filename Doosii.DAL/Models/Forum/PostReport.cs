using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Bao cao vi pham bai dang Forum.
    /// Placeholder cho task tuong lai.
    /// Schema: forum.
    /// </summary>
    [Table("PostReports", Schema = "forum")]
    public class PostReport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ReportedByUserId { get; set; }

        /// <summary>
        /// Loai bai dang: PassPost | SpotSharingPost.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string PostType { get; set; } = string.Empty;

        [Required]
        public int PostId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Trang thai xu ly: PENDING | RESOLVED | REJECTED.
        /// </summary>
        [MaxLength(20)]
        public string Status { get; set; } = "PENDING";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        // Navigation
        [ForeignKey(nameof(ReportedByUserId))]
        public User ReportedByUser { get; set; } = null!;
    }
}
