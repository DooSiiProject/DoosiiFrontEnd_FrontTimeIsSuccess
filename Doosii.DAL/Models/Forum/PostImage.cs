using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Anh cua mot bai PassPost.
    /// Schema: forum.
    /// </summary>
    [Table("PostImages", Schema = "forum")]
    public class PostImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int PostId { get; set; }

        /// <summary>
        /// HTTPS URL cua anh tren Cloudinary.
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Public ID tren Cloudinary, dung de xoa anh khi can.
        /// </summary>
        [MaxLength(500)]
        public string? PublicId { get; set; }

        /// <summary>
        /// Thu tu hien thi cua anh (0-based). Anh dau tien la thumbnail.
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        // Navigation
        [ForeignKey(nameof(PostId))]
        public PassPost Post { get; set; } = null!;
    }
}
