using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Cam xuc (LIKE / HEART) tren bai viet Forum.
    /// Schema: forum.
    /// </summary>
    [Table("Reactions", Schema = "forum")]
    public class Reaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// ID bai viet Forum.
        /// </summary>
        [Required]
        public int PostId { get; set; }

        /// <summary>
        /// ID nguoi dung da tha cam xuc (FK den auth.Users).
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Loai reaction: LIKE hoac HEART.
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
