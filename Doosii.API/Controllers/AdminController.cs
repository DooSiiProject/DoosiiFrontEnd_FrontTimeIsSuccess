using Doosii.BLL.Exceptions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.Common;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [CommunityValidation]
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IPostReportService _postReportService;
        private readonly ICommunityInteractionService _interactions;

        public AdminController(IAdminService adminService, IPostReportService postReportService, ICommunityInteractionService interactions)
        {
            _adminService = adminService;
            _postReportService = postReportService;
            _interactions = interactions;
        }

        /// <summary>
        /// Lấy danh sách hồ sơ đăng ký mở shop (KYC) (Lọc theo trạng thái: PENDING | APPROVED | REJECTED)
        /// </summary>
        [HttpGet("kyc-requests")]
        public async Task<IActionResult> GetKycRequests([FromQuery] string? status)
        {
            try
            {
                var result = await _adminService.GetKycRequestsAsync(status);
                return Ok(ApiResponse<List<MerchantProfileDto>>.SuccessResponse(result, "Lấy danh sách yêu cầu KYC thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<MerchantProfileDto>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy danh sách các đơn mở shop chưa duyệt (status: PENDING)
        /// </summary>
        [HttpGet("kyc-requests/pending")]
        public async Task<IActionResult> GetPendingKycRequests()
        {
            try
            {
                var result = await _adminService.GetKycRequestsAsync("PENDING");
                return Ok(ApiResponse<List<MerchantProfileDto>>.SuccessResponse(result, "Lấy danh sách đơn mở shop chưa duyệt thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<MerchantProfileDto>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết một đơn mở shop theo ID
        /// </summary>
        [HttpGet("kyc-requests/{id:int}")]
        public async Task<IActionResult> GetKycRequestById(int id)
        {
            try
            {
                var result = await _adminService.GetKycRequestByIdAsync(id);
                return Ok(ApiResponse<MerchantProfileDto>.SuccessResponse(result, "Lấy thông tin chi tiết đơn mở shop thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<MerchantProfileDto>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Phê duyệt đơn mở shop (Cập nhật status sang APPROVED, tự động nâng quyền Seller)
        /// </summary>
        [HttpPut("kyc-requests/{id:int}/approve")]
        public async Task<IActionResult> ApproveKycRequest(int id, [FromBody] ApproveKycRequest request)
        {
            try
            {
                var result = await _adminService.ApproveKycRequestAsync(id, request);
                return Ok(ApiResponse<MerchantProfileDto>.SuccessResponse(result, "Phê duyệt đơn mở shop thành công. Shop đã được tạo và tài khoản đã được nâng cấp thành Shopper."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<MerchantProfileDto>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Từ chối đơn mở shop (Cập nhật status sang REJECTED kèm lý do)
        /// </summary>
        [HttpPut("kyc-requests/{id:int}/reject")]
        public async Task<IActionResult> RejectKycRequest(int id, [FromBody] RejectKycRequest? request)
        {
            try
            {
                var result = await _adminService.RejectKycRequestAsync(id, request?.Reason);
                return Ok(ApiResponse<MerchantProfileDto>.SuccessResponse(result, "Đã từ chối đơn mở shop."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<MerchantProfileDto>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Phê duyệt hoặc từ chối hồ sơ đăng ký mở shop của người bán (Tương thích ngược)
        /// </summary>
        [HttpPost("kyc-requests/{id:int}/verdict")]
        public async Task<IActionResult> ReviewKycRequest(int id, [FromBody] ReviewKycRequest request)
        {
            try
            {
                var result = await _adminService.ReviewKycRequestAsync(id, request);
                var message = request.IsApproved
                    ? "Phê duyệt hồ sơ KYC thành công. Shop đã được tạo và tài khoản đã được nâng cấp thành Shopper."
                    : "Đã từ chối hồ sơ KYC.";
                return Ok(ApiResponse<MerchantProfileDto>.SuccessResponse(result, message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<MerchantProfileDto>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy danh sách khiếu nại đơn hàng ký quỹ (Lọc theo trạng thái: PENDING_REVIEW | REFUNDED_BUYER | RELEASED_SELLER)
        /// </summary>
        [HttpGet("disputes")]
        public async Task<IActionResult> GetDisputes([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
                await _interactions.EnsureAdminAsync(actor);
                return Ok(Doosii.BLL.DTOs.ApiResponse.Ok(await _adminService.GetDisputesAsync(status, page, pageSize)));
            }
            catch (Doosii.BLL.Exceptions.ServiceException ex) { return StatusCode(ex.StatusCode, Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (ArgumentException ex) { return BadRequest(Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (Exception) { return StatusCode(500, Doosii.BLL.DTOs.ApiResponse.Fail("Không tải được danh sách khiếu nại.")); }
        }

        [HttpPost("disputes/{id:int}/arbitrate")]
        public async Task<IActionResult> ArbitrateDispute(int id, [FromBody] ArbitrateDisputeRequest request)
        {
            try
            {
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
                await _interactions.EnsureAdminAsync(actor);
                return Ok(Doosii.BLL.DTOs.ApiResponse.Ok(await _adminService.ArbitrateDisputeAsync(id, request), "Đã cập nhật kết quả xử lý khiếu nại."));
            }
            catch (Doosii.BLL.Exceptions.ServiceException ex) { return StatusCode(ex.StatusCode, Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (KeyNotFoundException ex) { return NotFound(Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (ArgumentException ex) { return BadRequest(Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) when (!Doosii.BLL.Helpers.CommunityDatabaseErrors.IsConflict(ex) && ex.InnerException == null) { return Conflict(Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (Exception ex) when (Doosii.BLL.Helpers.CommunityDatabaseErrors.IsConflict(ex) || ex is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            { return Conflict(Doosii.BLL.DTOs.ApiResponse.Fail("Đơn hàng vừa thay đổi. Vui lòng tải lại.")); }
            catch (Exception) { return StatusCode(500, Doosii.BLL.DTOs.ApiResponse.Fail("Chưa thể xử lý khiếu nại. Vui lòng tải lại.")); }
        }

        /// <summary>
        /// Lấy danh sách yêu cầu rút tiền của các shop (Lọc theo trạng thái: PENDING | APPROVED | REJECTED)
        /// </summary>
        [HttpGet("withdrawals")]
        public async Task<IActionResult> GetWithdrawals([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try {
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
                await _interactions.EnsureAdminAsync(actor);
                return Ok(Doosii.BLL.DTOs.ApiResponse.Ok(await _adminService.GetWithdrawalsAsync(status, page, pageSize)));
            }
            catch (ServiceException ex) { return StatusCode(ex.StatusCode, Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (ArgumentException ex) { return BadRequest(Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (Exception) { return StatusCode(500, Doosii.BLL.DTOs.ApiResponse.Fail("Không tải được yêu cầu rút tiền.")); }
        }
        [HttpPost("withdrawals/{id:int}/process")]
        public async Task<IActionResult> ProcessWithdrawal(int id, [FromBody] ProcessWithdrawalRequest request)
        {
            try {
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
                await _interactions.EnsureAdminAsync(actor);
                return Ok(Doosii.BLL.DTOs.ApiResponse.Ok(await _adminService.ProcessWithdrawalAsync(id, request, actor), "Đã ghi nhận quyết định trong hệ thống; không thực hiện chuyển khoản ngân hàng."));
            }
            catch (ServiceException ex) { return StatusCode(ex.StatusCode, Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (KeyNotFoundException ex) { return NotFound(Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (ArgumentException ex) { return BadRequest(Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (Exception ex) when (Doosii.BLL.Helpers.CommunityDatabaseErrors.IsConflict(ex)) { return Conflict(Doosii.BLL.DTOs.ApiResponse.Fail("Dữ liệu vừa thay đổi. Hãy tải lại.")); }
            catch (Exception) { return StatusCode(500, Doosii.BLL.DTOs.ApiResponse.Fail("Chưa thể xử lý yêu cầu. Hãy tải lại để kiểm tra kết quả.")); }
        }

        /// <summary>
        /// Lấy số liệu thống kê tài chính và vận hành tổng quan (GMV, Doanh thu phí ký quỹ, số đơn hàng, số shop)
        /// </summary>
        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
        {
            try {
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
                await _interactions.EnsureAdminAsync(actor);
                Response.Headers.CacheControl = "no-store";
                return Ok(Doosii.BLL.DTOs.ApiResponse.Ok(await _adminService.GetAnalyticsAsync()));
            }
            catch (ServiceException ex) { return StatusCode(ex.StatusCode, Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message)); }
            catch (Exception) { return StatusCode(500, Doosii.BLL.DTOs.ApiResponse.Fail("Không tải được thống kê. Vui lòng thử lại.")); }
        }

        // =========================================================================
        // FORUM POST REPORTS & MODERATION QUEUE (Track 1 - Tri)
        // =========================================================================

        /// <summary>
        /// Lay danh sach moderation queue bao cao bai viet Forum.
        /// Chi danh cho Admin.
        /// GET /api/admin/reports
        /// </summary>
        [HttpGet("reports")]
        public async Task<IActionResult> GetReportsQueue([FromQuery] PostReportQuery query)
        {
            try
            {
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
                await _interactions.EnsureAdminAsync(actorId);
                var result = await _postReportService.GetReportsQueueAsync(query);
                return Ok(Doosii.BLL.DTOs.ApiResponse.Ok(result, "Moderation queue retrieved successfully"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, Doosii.BLL.DTOs.ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Xu ly bao cao bai viet Forum (DISMISS, HIDE_POST, DELETE_POST, BAN_AUTHOR).
        /// Chi danh cho Admin.
        /// PUT /api/admin/reports/{reportId}/resolve
        /// </summary>
        [HttpPut("reports/{reportId:int}/resolve")]
        public async Task<IActionResult> ResolveReport(int reportId, [FromBody] ResolvePostReportRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(Doosii.BLL.DTOs.ApiResponse.Fail("Invalid request data", errors));
            }

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminIdClaim) || !int.TryParse(adminIdClaim, out int adminId))
            {
                return Unauthorized(Doosii.BLL.DTOs.ApiResponse.Fail("User is not authenticated", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var result = await _postReportService.ResolveReportAsync(reportId, adminId, request);
                return Ok(Doosii.BLL.DTOs.ApiResponse.Ok(result, "Report resolved successfully"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, Doosii.BLL.DTOs.ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, Doosii.BLL.DTOs.ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
        }
    }
}
