using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Cam xuc (reaction / like) cua nguoi dung tren bai dang Forum.
    /// Placeholder cho task tuong lai.
    /// Schema: forum.
    /// </summary>
    [Table("Reactions", Schema = "forum")]
    public class Reaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Loai bai dang: PassPost | SpotSharingPost.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string PostType { get; set; } = string.Empty;

        [Required]
        public int PostId { get; set; }

        /// <summary>
        /// Loai reaction: LIKE | LOVE | HAHA | SAD | ANGRY.
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string ReactionType { get; set; } = "LIKE";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;
    }
}
