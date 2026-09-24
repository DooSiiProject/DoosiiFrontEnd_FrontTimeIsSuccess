using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.Common;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claim, out int userId))
            {
                return userId;
            }
            return null;
        }

        /// <summary>
        /// Lấy danh sách thông báo của người dùng hiện tại (hỗ trợ lọc unreadOnly)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool? unreadOnly)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<List<NotificationDto>>.ErrorResponse("Token không hợp lệ."));
            }

            try
            {
                var result = await _notificationService.GetUserNotificationsAsync(userId.Value, unreadOnly);
                return Ok(ApiResponse<List<NotificationDto>>.SuccessResponse(result, "Lấy danh sách thông báo thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<NotificationDto>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy số lượng thông báo chưa đọc của người dùng hiện tại
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<int>.ErrorResponse("Token không hợp lệ."));
            }

            try
            {
                var count = await _notificationService.GetUnreadCountAsync(userId.Value);
                return Ok(ApiResponse<int>.SuccessResponse(count, "Lấy số lượng thông báo chưa đọc thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<int>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Đánh dấu một thông báo là đã đọc
        /// </summary>
        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<bool>.ErrorResponse("Token không hợp lệ."));
            }

            try
            {
                await _notificationService.MarkAsReadAsync(id, userId.Value);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Đã đánh dấu thông báo là đã đọc."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Đánh dấu tất cả thông báo là đã đọc
        /// </summary>
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<bool>.ErrorResponse("Token không hợp lệ."));
            }

            try
            {
                await _notificationService.MarkAllAsReadAsync(userId.Value);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Đã đánh dấu tất cả thông báo là đã đọc."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }
    }
}
