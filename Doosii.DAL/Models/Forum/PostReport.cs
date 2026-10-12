using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Bao cao vi pham bai dang Forum (PassPost hoac SpotSharingPost).
    /// Schema: forum.
    /// </summary>
    [Table("PostReports", Schema = "forum")]
    public class PostReport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// ID bai viet bi bao cao.
        /// </summary>
        [Required]
        public int PostId { get; set; }

        /// <summary>
        /// Loai bai viet: PASS | SPOT.
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string PostType { get; set; } = string.Empty;

        /// <summary>
        /// Nguoi dung gui bao cao (FK den auth.Users).
        /// </summary>
        [Required]
        public int ReporterId { get; set; }

        /// <summary>
        /// Ly do bao cao: SCAM | COUNTERFEIT | OFFENSIVE | PROHIBITED_CONTENT | OTHER.
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Mo ta chi tiet vi pham, toi da 1000 ky tu.
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Chuoi JSON chua toi da 5 URL bang chung.
        /// </summary>
        [MaxLength(3000)]
        public string? EvidenceUrlsJson { get; set; }

        /// <summary>
        /// Trang thai xu ly: PENDING | REVIEWED | DISMISSED | ACTION_TAKEN.
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "PENDING";

        /// <summary>
        /// Admin da xu ly bao cao nay (FK den auth.Users). Null neu chua xu ly.
        /// </summary>
        public int? ReviewedByAdminId { get; set; }

        /// <summary>
        /// Ghi chu cua Admin khi xu ly bao cao.
        /// </summary>
        [MaxLength(2000)]
        public string? AdminNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(ReporterId))]
        public User Reporter { get; set; } = null!;

        [ForeignKey(nameof(ReviewedByAdminId))]
        public User? ReviewedByAdmin { get; set; }
    }
}
