using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Binh luan cua nguoi dung duoi bai dang Forum.
    /// Placeholder cho task tuong lai.
    /// Schema: forum.
    /// </summary>
    [Table("Comments", Schema = "forum")]
    public class Comment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int AuthorId { get; set; }

        /// <summary>
        /// Loai bai dang: PassPost | SpotSharingPost.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string PostType { get; set; } = string.Empty;

        [Required]
        public int PostId { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        public int? ParentCommentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation
        [ForeignKey(nameof(AuthorId))]
        public User Author { get; set; } = null!;
    }
}
