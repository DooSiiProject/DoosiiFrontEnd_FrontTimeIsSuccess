using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Bai dang chia se dia diem (spot sharing) trong Forum.
    /// User co the tag mot Store da co trong he thong hoac nhap toa do thu cong.
    /// It nhat mot trong hai phai co: TaggedStoreId hoac (Latitude + Longitude).
    /// Schema: forum.
    /// </summary>
    [Table("SpotSharingPosts", Schema = "forum")]
    public class SpotSharingPost
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Nguoi dang bai - FK den auth.Users.
        /// </summary>
        [Required]
        public int AuthorId { get; set; }

        /// <summary>
        /// Store duoc tag trong bai. Nullable - user co the dung toa do thu cong.
        /// </summary>
        public int? TaggedStoreId { get; set; }

        /// <summary>
        /// Vi do (lat) thu cong, tu -90 den 90. Nullable.
        /// </summary>
        [Column(TypeName = "decimal(10, 7)")]
        public decimal? Latitude { get; set; }

        /// <summary>
        /// Kinh do (lng) thu cong, tu -180 den 180. Nullable.
        /// </summary>
        [Column(TypeName = "decimal(10, 7)")]
        public decimal? Longitude { get; set; }

        /// <summary>
        /// Ten dia diem tu nhap, khong bat buoc, toi da 200 ky tu.
        /// </summary>
        [MaxLength(200)]
        public string? LocationName { get; set; }

        /// <summary>
        /// Noi dung bai dang, bat buoc, toi da 2000 ky tu.
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Trang thai bai viet: ACTIVE, HIDDEN.
        /// </summary>
        [MaxLength(20)]
        public string Status { get; set; } = "ACTIVE";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(AuthorId))]
        public User Author { get; set; } = null!;

        [ForeignKey(nameof(TaggedStoreId))]
        public Store.Store? TaggedStore { get; set; }

        public ICollection<SpotImage> Images { get; set; } = new List<SpotImage>();
    }
}
