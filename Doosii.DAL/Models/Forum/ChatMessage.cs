using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Doosii.DAL.Models.Store;

namespace Doosii.DAL.Models.Forum
{
    /// <summary>
    /// Tin nhan trong conversation giua buyer va seller.
    /// Schema: forum.
    /// Khong xoa vat ly de bao ton chat log (phuc vu dispute resolution).
    /// </summary>
    [Table("ChatMessages", Schema = "forum")]
    public class ChatMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Conversation chua tin nhan nay.
        /// </summary>
        [Required]
        public int ConversationId { get; set; }

        /// <summary>
        /// Nguoi gui - phai la Buyer hoac Seller cua conversation.
        /// FK den auth.Users.
        /// </summary>
        [Required]
        public int SenderId { get; set; }

        /// <summary>
        /// Noi dung tin nhan. Bat buoc voi TEXT, toi da 2000 ky tu.
        /// </summary>
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Product duoc dinh kem (khi MessageType = PRODUCT_CARD).
        /// FK den store.Products. Nullable.
        /// </summary>
        public int? ProductId { get; set; }

        /// <summary>
        /// Loai tin nhan: TEXT | PRODUCT_CARD | SYSTEM.
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string MessageType { get; set; } = "TEXT";

        public Guid? ClientRequestId { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Da doc boi nguoi nhan chua.
        /// </summary>
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// Xoa mem - chat log van ton tai trong DB.
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        [ForeignKey(nameof(ConversationId))]
        public Conversation Conversation { get; set; } = null!;

        [ForeignKey(nameof(SenderId))]
        public User Sender { get; set; } = null!;

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }
    }
}

