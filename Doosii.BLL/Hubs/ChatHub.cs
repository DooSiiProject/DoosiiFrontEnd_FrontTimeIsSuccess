using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;

namespace Doosii.BLL.Hubs
{
    /// <summary>
    /// SignalR Hub cho tinh nang nhan tin 1-1 giua buyer va seller.
    /// JWT authentication bat buoc.
    /// JWT co the truyen qua Authorization header hoac query string ?access_token=...
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        // =====================================================================
        // LIFECYCLE
        // =====================================================================

        public override async Task OnConnectedAsync()
        {
            var userId = GetCurrentUserId();
            if (userId != null)
            {
                // Tham gia group ca nhan de nhan notification
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetCurrentUserId();
            if (userId != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        // =====================================================================
        // CLIENT -> SERVER METHODS
        // =====================================================================

        /// <summary>
        /// Client tham gia vao group cua conversation.
        /// Chi thanh vien moi duoc join.
        /// </summary>
        public async Task JoinConversation(int conversationId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "UNAUTHORIZED", Message = "Not authenticated" });
                return;
            }

            try
            {
                var isMember = await _chatService.IsMemberAsync(conversationId, userId.Value);
                if (!isMember)
                {
                    await Clients.Caller.SendAsync("ChatError", new { Code = "CHAT_ACCESS_DENIED", Message = "You are not a member of this conversation" });
                    return;
                }

                var groupName = $"conversation_{conversationId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                await Clients.Group(groupName).SendAsync("UserJoined", new { UserId = userId.Value, ConversationId = conversationId });
            }
            catch (ServiceException ex)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = ex.ErrorCode, Message = ex.Message });
            }
            catch
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "MESSAGE_SEND_FAILED", Message = "An error occurred joining the conversation" });
            }
        }

        /// <summary>
        /// Client roi khoi group cua conversation.
        /// </summary>
        public async Task LeaveConversation(int conversationId)
        {
            var userId = GetCurrentUserId();
            var groupName = $"conversation_{conversationId}";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            if (userId != null)
            {
                await Clients.Group(groupName).SendAsync("UserLeft", new { UserId = userId.Value, ConversationId = conversationId });
            }
        }

        /// <summary>
        /// Client gui TEXT message.
        /// Luu DB truoc, broadcast sau. Neu luu loi thi khong broadcast.
        /// </summary>
        public async Task SendMessage(int conversationId, string content)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "UNAUTHORIZED", Message = "Not authenticated" });
                return;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "INVALID_MESSAGE", Message = "Message content cannot be empty" });
                return;
            }

            if (content.Length > 2000)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "INVALID_MESSAGE", Message = "Message content cannot exceed 2000 characters" });
                return;
            }

            try
            {
                // Luu DB truoc
                var messageDto = await _chatService.SendTextMessageAsync(conversationId, userId.Value, content);

                // Broadcast sau khi luu thanh cong
                var groupName = $"conversation_{conversationId}";
                await Clients.Group(groupName).SendAsync("ReceiveMessage", messageDto);
            }
            catch (ServiceException ex)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = ex.ErrorCode, Message = ex.Message });
            }
            catch
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "MESSAGE_SEND_FAILED", Message = "Failed to send message. Please try again." });
            }
        }

        /// <summary>
        /// Client gui PRODUCT_CARD message.
        /// </summary>
        public async Task SendProductCard(int conversationId, int productId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "UNAUTHORIZED", Message = "Not authenticated" });
                return;
            }

            try
            {
                var messageDto = await _chatService.SendProductCardAsync(conversationId, userId.Value, productId);

                var groupName = $"conversation_{conversationId}";
                await Clients.Group(groupName).SendAsync("ReceiveProductCard", messageDto);
            }
            catch (ServiceException ex)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = ex.ErrorCode, Message = ex.Message });
            }
            catch
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "MESSAGE_SEND_FAILED", Message = "Failed to send product card. Please try again." });
            }
        }

        /// <summary>
        /// Client danh dau message da doc.
        /// Phat su kien MessagesRead cho thanh vien con lai.
        /// </summary>
        public async Task MarkMessagesAsRead(int conversationId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "UNAUTHORIZED", Message = "Not authenticated" });
                return;
            }

            try
            {
                var status = await _chatService.MarkMessagesAsReadAsync(conversationId, userId.Value);

                // Thong bao cho nguoi gui goc (nguoi kia) biet tin nhan da duoc doc
                await Clients.Group($"conversation_{conversationId}").SendAsync("MessagesRead", status);
            }
            catch (ServiceException ex)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = ex.ErrorCode, Message = ex.Message });
            }
            catch
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "MESSAGE_SEND_FAILED", Message = "An error occurred" });
            }
        }

        /// <summary>
        /// Client thong bao trang thai typing (dang nhap tin nhan).
        /// Phat su kien UserTyping cho thanh vien con lai trong phong chat.
        /// </summary>
        public async Task SendTyping(int conversationId, bool isTyping)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                await Clients.Caller.SendAsync("ChatError", new { Code = "UNAUTHORIZED", Message = "Not authenticated" });
                return;
            }

            var typingDto = new UserTypingDto
            {
                ConversationId = conversationId,
                UserId = userId.Value,
                IsTyping = isTyping
            };

            await Clients.OthersInGroup($"conversation_{conversationId}").SendAsync("UserTyping", typingDto);
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        private int? GetCurrentUserId()
        {
            var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claim, out int userId)) return userId;
            return null;
        }
    }
}
