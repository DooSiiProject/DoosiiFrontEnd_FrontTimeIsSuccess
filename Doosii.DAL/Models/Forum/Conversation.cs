using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Doosii.DAL.Models.Store;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Cuoc tro chuyen 1-1 giua buyer va seller.
    /// Schema: forum.
    /// </summary>
    [Table("Conversations", Schema = "forum")]
    public class Conversation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Nguoi mua - FK den auth.Users.
        /// </summary>
        [Required]
        public int BuyerId { get; set; }

        /// <summary>
        /// Nguoi ban - FK den auth.Users.
        /// </summary>
        [Required]
        public int SellerId { get; set; }

        /// <summary>
        /// Product lien quan (co the null neu chat chung).
        /// FK den store.Products.
        /// </summary>
        public int? ProductId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Thoi diem co message cuoi cung (dung de sort inbox).
        /// </summary>
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Conversation bi dong se khong nhan them message.
        /// </summary>
        public bool IsClosed { get; set; } = false;

        // Navigation properties
        [ForeignKey(nameof(BuyerId))]
        public User Buyer { get; set; } = null!;

        [ForeignKey(nameof(SellerId))]
        public User Seller { get; set; } = null!;

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
