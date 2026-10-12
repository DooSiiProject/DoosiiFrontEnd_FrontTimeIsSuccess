using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Hubs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(IChatService chatService, IHubContext<ChatHub> hubContext)
        {
            _chatService = chatService;
            _hubContext = hubContext;
        }

        // =========================================================================
        // CONVERSATIONS
        // =========================================================================

        /// <summary>
        /// Tao hoac lay conversation hien co giua buyer (current user) va seller.
        /// POST /api/chat/conversations
        /// </summary>
        [HttpPost("conversations")]
        public async Task<IActionResult> CreateOrGetConversation([FromBody] CreateConversationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Invalid request data", GetModelErrors()));
            }

            var buyerId = GetCurrentUserId();
            if (buyerId == null)
            {
                return Unauthorized(ApiResponse.Fail("Not authenticated", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var result = await _chatService.GetOrCreateConversationAsync(buyerId.Value, request.SellerId, request.ProductId);
                return Ok(ApiResponse.Ok(result, "Conversation ready"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lay danh sach conversations cua user hien tai.
        /// Sort theo LastMessageAt DESC, phan trang.
        /// GET /api/chat/conversations
        /// </summary>
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations([FromQuery] GetConversationsQuery query)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Not authenticated", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var result = await _chatService.GetConversationsAsync(userId.Value, query);
                return Ok(ApiResponse.Ok(result, "Conversations retrieved successfully"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
        }

        // =========================================================================
        // CHAT HISTORY
        // =========================================================================

        /// <summary>
        /// Lay lich su tin nhan cua mot conversation.
        /// Chi thanh vien moi duoc xem.
        /// GET /api/chat/conversations/{conversationId}/messages
        /// </summary>
        [HttpGet("conversations/{conversationId:int}/messages")]
        public async Task<IActionResult> GetMessages(int conversationId, [FromQuery] GetMessagesQuery query)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Not authenticated", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var result = await _chatService.GetMessagesAsync(conversationId, userId.Value, query);
                return Ok(ApiResponse.Ok(result, "Messages retrieved successfully"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
        }

        // =========================================================================
        // MARK AS READ (REST fallback; real-time dung SignalR)
        // =========================================================================

        /// <summary>
        /// Danh dau message cua nguoi kia la da doc.
        /// Phat su kien MessagesRead qua SignalR cho conversation.
        /// PUT /api/chat/conversations/{conversationId}/read
        /// </summary>
        [HttpPut("conversations/{conversationId:int}/read")]
        public async Task<IActionResult> MarkAsRead(int conversationId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Not authenticated", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var status = await _chatService.MarkMessagesAsReadAsync(conversationId, userId.Value);

                // Phat su kien SignalR cho ca hai thanh vien
                await _hubContext.Clients
                    .Group($"conversation_{conversationId}")
                    .SendAsync("MessagesRead", status);

                return Ok(ApiResponse.Ok(status, "Messages marked as read"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
        }

        // =========================================================================
        // REST SEND (fallback ngoai SignalR)
        // =========================================================================

        /// <summary>
        /// Gui TEXT message qua REST (fallback khi WebSocket khong kha dung).
        /// POST /api/chat/conversations/{conversationId}/messages
        /// </summary>
        [HttpPost("conversations/{conversationId:int}/messages")]
        public async Task<IActionResult> SendMessage(int conversationId, [FromBody] SendMessageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Invalid request data", GetModelErrors()));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Not authenticated", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var messageDto = await _chatService.SendTextMessageAsync(conversationId, userId.Value, request.Content);

                // Broadcast qua SignalR
                await _hubContext.Clients
                    .Group($"conversation_{conversationId}")
                    .SendAsync("ReceiveMessage", messageDto);

                return Ok(ApiResponse.Ok(messageDto, "Message sent successfully"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Gui PRODUCT_CARD message qua REST.
        /// POST /api/chat/conversations/{conversationId}/product-card
        /// </summary>
        [HttpPost("conversations/{conversationId:int}/product-card")]
        public async Task<IActionResult> SendProductCard(int conversationId, [FromBody] SendProductCardRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Invalid request data", GetModelErrors()));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Not authenticated", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var messageDto = await _chatService.SendProductCardAsync(conversationId, userId.Value, request.ProductId);

                await _hubContext.Clients
                    .Group($"conversation_{conversationId}")
                    .SendAsync("ReceiveProductCard", messageDto);

                return Ok(ApiResponse.Ok(messageDto, "Product card sent successfully"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
        }

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claim, out int id)) return id;
            return null;
        }

        private List<string> GetModelErrors() =>
            ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
    }
}
