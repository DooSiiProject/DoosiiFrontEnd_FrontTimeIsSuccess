using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    // =========================================================================
    // REQUEST DTOs
    // =========================================================================

    public class CreateConversationRequest
    {
        [Required(ErrorMessage = "SellerId is required")]
        public int SellerId { get; set; }

        public int? ProductId { get; set; }
    }

    public class SendMessageRequest
    {
        [Required(ErrorMessage = "Content is required")]
        [MaxLength(2000, ErrorMessage = "Content cannot exceed 2000 characters")]
        public string Content { get; set; } = string.Empty;
    }

    public class SendProductCardRequest
    {
        [Required(ErrorMessage = "ProductId is required")]
        public int ProductId { get; set; }
    }

    public class GetMessagesQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 30;
    }

    public class GetConversationsQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // =========================================================================
    // RESPONSE DTOs
    // =========================================================================

    public class ConversationDto
    {
        public int Id { get; set; }
        public int BuyerId { get; set; }
        public int SellerId { get; set; }
        public int? ProductId { get; set; }
        public bool IsClosed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastMessageAt { get; set; }

        public UserSummaryDto Buyer { get; set; } = null!;
        public UserSummaryDto Seller { get; set; } = null!;
        public ProductCardDto? Product { get; set; }

        // Inbox preview
        public ChatMessageDto? LastMessage { get; set; }
        public int UnreadCount { get; set; }
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderAvatar { get; set; }
        public string Content { get; set; } = string.Empty;
        public int? ProductId { get; set; }
        public string MessageType { get; set; } = "TEXT";
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public bool IsDeleted { get; set; }

        // Only set when MessageType == PRODUCT_CARD
        public ProductCardDto? ProductCard { get; set; }
    }

    public class ProductCardDto
    {
        public int ProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
    }

    public class ReadStatusDto
    {
        public int ConversationId { get; set; }
        public int UnreadCount { get; set; }
        public int MarkedCount { get; set; }
    }
}
