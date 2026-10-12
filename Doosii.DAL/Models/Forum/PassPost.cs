using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Bai dang pass do (second-hand item listing) trong Forum.
    /// Schema: forum.
    /// </summary>
    [Table("PassPosts", Schema = "forum")]
    public class PassPost
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// UserId cua nguoi dang bai.
        /// </summary>
        [Required]
        public int AuthorId { get; set; }

        /// <summary>
        /// Tieu de bai dang. Toi da 150 ky tu.
        /// </summary>
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Mo ta chi tiet san pham.
        /// </summary>
        [Required]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gia san pham. Phai lon hon 0.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        /// <summary>
        /// Kich co san pham (S, M, L, XL, ...).
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Size { get; set; } = string.Empty;

        /// <summary>
        /// Tinh trang san pham tu 0 (moi 100%) den 100 (100%).
        /// </summary>
        [Range(0, 100)]
        public int ConditionPercent { get; set; } = 100;

        /// <summary>
        /// Cho phep giao dich qua he thong Escrow cua Doosii hay khong.
        /// </summary>
        public bool AllowEscrow { get; set; } = true;

        /// <summary>
        /// Trang thai bai dang: ACTIVE | LOCKED | SOLD | HIDDEN.
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "ACTIVE";

        /// <summary>
        /// Danh sach style tag (JSON array hoac comma-separated).
        /// </summary>
        [MaxLength(500)]
        public string? StyleTags { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(AuthorId))]
        public User Author { get; set; } = null!;

        public ICollection<PostImage> Images { get; set; } = new List<PostImage>();
    }
}
