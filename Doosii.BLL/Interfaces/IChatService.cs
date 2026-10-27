using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IChatService
    {
        /// <summary>
        /// Tao hoac lay conversation hien co giua buyer va seller (co the theo product).
        /// </summary>
        Task<ConversationDto> GetConversationAsync(int conversationId, int userId);
        Task<ConversationDto> GetOrCreateConversationAsync(int buyerId, int sellerId, int? productId);

        /// <summary>
        /// Lay danh sach conversations cua user hien tai (phan trang, sort by LastMessageAt DESC).
        /// </summary>
        Task<PagedResult<ConversationDto>> GetConversationsAsync(int userId, GetConversationsQuery query);

        /// <summary>
        /// Lay lich su chat cua mot conversation (phan trang, sort by SentAt ASC).
        /// Kiem tra quyen truy cap.
        /// </summary>
        Task<ChatMessagesPageDto> GetMessagesAsync(int conversationId, int userId, GetMessagesQuery query);

        /// <summary>
        /// Gui TEXT message, luu DB va tra DTO de broadcast.
        /// </summary>
        Task<ChatMessageDto> SendTextMessageAsync(int conversationId, int senderId, string content, Guid? clientRequestId = null);

        /// <summary>
        /// Gui PRODUCT_CARD message, luu DB va tra DTO de broadcast.
        /// </summary>
        Task<ChatMessageDto> SendProductCardAsync(int conversationId, int senderId, int productId, Guid? clientRequestId = null);

        /// <summary>
        /// Danh dau cac message cua nguoi kia la da doc.
        /// Tra ve ReadStatusDto de phat SignalR event.
        /// </summary>
        Task<ReadStatusDto> MarkMessagesAsReadAsync(int conversationId, int userId, int? upToId = null);

        /// <summary>
        /// Kiem tra user co la thanh vien cua conversation (buyer hoac seller).
        /// </summary>
        Task<bool> IsMemberAsync(int conversationId, int userId);

        /// <summary>
        /// Lay thong tin ProductCard.
        /// </summary>
        Task<ProductCardDto?> GetProductCardAsync(int productId);
    }
}

