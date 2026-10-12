using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Binh luan trong Forum (ho tro toi da 2 cap: root comment va reply).
    /// Schema: forum.
    /// </summary>
    [Table("Comments", Schema = "forum")]
    public class Comment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// ID cua bai viet Forum (PassPost hoac SpotSharingPost).
        /// </summary>
        [Required]
        public int PostId { get; set; }

        /// <summary>
        /// ID tac gia binh luan (FK den auth.Users).
        /// </summary>
        [Required]
        public int AuthorId { get; set; }

        /// <summary>
        /// ID binh luan cha. Null neu la comment goc (cap 1).
        /// </summary>
        public int? ParentCommentId { get; set; }

        /// <summary>
        /// Noi dung binh luan, bat buoc, toi da 1000 ky tu.
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Danh dau xoa mem (soft delete). Mac dinh false.
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        [ForeignKey(nameof(AuthorId))]
        public User Author { get; set; } = null!;

        [ForeignKey(nameof(ParentCommentId))]
        public Comment? ParentComment { get; set; }

        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    }
}
