using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Doosii.DAL.Models.Order;

namespace Doosii.DAL.Models.Store
{
    [Table("StoreReviews", Schema = "store")]
    public class StoreReview
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int StoreId { get; set; }

        [Required]
        public int UserId { get; set; }

        public int? OrderId { get; set; }

        /// <summary>
        /// Rating tu 1 den 5 sao.
        /// </summary>
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(2000)]
        public string? Comment { get; set; }

        /// <summary>
        /// Chuoi JSON chua toi da 3 link anh danh gia.
        /// </summary>
        [MaxLength(2000)]
        public string? ImagesJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(StoreId))]
        public Store Store { get; set; } = null!;

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [ForeignKey(nameof(OrderId))]
        public Order.Order? Order { get; set; }
    }
}
