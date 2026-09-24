using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.Common;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    public class AnnouncementsController : ControllerBase
    {
        private readonly IAnnouncementService _announcementService;

        public AnnouncementsController(IAnnouncementService announcementService)
        {
            _announcementService = announcementService;
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
        /// Người bán mua gói phát sóng thông báo khui kiện (50,000 VND, tối đa 2 lần/ngày)
        /// </summary>
        [HttpPost("api/seller/announcements")]
        [Authorize]
        public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementRequest request)
        {
            var sellerId = GetCurrentUserId();
            if (sellerId == null)
            {
                return Unauthorized(ApiResponse<AnnouncementResponse>.ErrorResponse("Token không hợp lệ."));
            }

            try
            {
                var result = await _announcementService.CreateAnnouncementAsync(sellerId.Value, request);
                return Ok(ApiResponse<AnnouncementResponse>.SuccessResponse(result, "Tạo thông báo khui kiện và phát sóng thành công!"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<AnnouncementResponse>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<AnnouncementResponse>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<AnnouncementResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<AnnouncementResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy danh sách các thông báo khui kiện của shop tôi
        /// </summary>
        [HttpGet("api/seller/announcements")]
        [Authorize]
        public async Task<IActionResult> GetMyAnnouncements()
        {
            var sellerId = GetCurrentUserId();
            if (sellerId == null)
            {
                return Unauthorized(ApiResponse<List<AnnouncementResponse>>.ErrorResponse("Token không hợp lệ."));
            }

            try
            {
                var result = await _announcementService.GetSellerAnnouncementsAsync(sellerId.Value);
                return Ok(ApiResponse<List<AnnouncementResponse>>.SuccessResponse(result, "Lấy danh sách thông báo khui kiện thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<AnnouncementResponse>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Khách hàng xem danh sách các buổi khui kiện sắp diễn ra trên toàn hệ thống
        /// </summary>
        [HttpGet("api/announcements/upcoming")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUpcomingAnnouncements()
        {
            try
            {
                var result = await _announcementService.GetUpcomingAnnouncementsAsync();
                return Ok(ApiResponse<List<AnnouncementResponse>>.SuccessResponse(result, "Lấy danh sách buổi khui kiện sắp tới thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<AnnouncementResponse>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }
    }
}
