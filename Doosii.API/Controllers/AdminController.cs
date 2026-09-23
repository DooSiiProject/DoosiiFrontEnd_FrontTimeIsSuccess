using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.Common;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
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
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<MerchantProfileDto>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Phê duyệt hoặc từ chối hồ sơ đăng ký mở shop của người bán
        /// </summary>
        [HttpPost("kyc-requests/{id:int}/verdict")]
        public async Task<IActionResult> ReviewKycRequest(int id, [FromBody] ReviewKycRequest request)
        {
            try
            {
                var result = await _adminService.ReviewKycRequestAsync(id, request);
                var message = request.IsApproved
                    ? "Phê duyệt hồ sơ KYC thành công. Người dùng đã được nâng cấp quyền Người bán (Seller)."
                    : "Đã từ chối hồ sơ KYC.";
                return Ok(ApiResponse<MerchantProfileDto>.SuccessResponse(result, message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<MerchantProfileDto>.ErrorResponse(ex.Message));
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
        public async Task<IActionResult> GetDisputes([FromQuery] string? status)
        {
            try
            {
                var result = await _adminService.GetDisputesAsync(status);
                return Ok(ApiResponse<List<DisputeResponse>>.SuccessResponse(result, "Lấy danh sách khiếu nại thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<DisputeResponse>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Phán quyết phân xử khiếu nại (Hoàn tiền 100% cho người mua hoặc giải ngân cho người bán)
        /// </summary>
        [HttpPost("disputes/{id:int}/arbitrate")]
        public async Task<IActionResult> ArbitrateDispute(int id, [FromBody] ArbitrateDisputeRequest request)
        {
            try
            {
                var result = await _adminService.ArbitrateDisputeAsync(id, request);
                var message = request.Verdict.Equals("REFUND_BUYER", StringComparison.OrdinalIgnoreCase)
                    ? "Phán quyết hoàn tiền thành công. Tiền ký quỹ đã được hoàn cho người mua và sản phẩm được mở khóa."
                    : "Phán quyết giải ngân thành công. Tiền ký quỹ đã được giải ngân cho người bán.";
                return Ok(ApiResponse<DisputeResponse>.SuccessResponse(result, message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<DisputeResponse>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<DisputeResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<DisputeResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy danh sách yêu cầu rút tiền của các shop (Lọc theo trạng thái: PENDING | APPROVED | REJECTED)
        /// </summary>
        [HttpGet("withdrawals")]
        public async Task<IActionResult> GetWithdrawals([FromQuery] string? status)
        {
            try
            {
                var result = await _adminService.GetWithdrawalsAsync(status);
                return Ok(ApiResponse<List<WithdrawalResponse>>.SuccessResponse(result, "Lấy danh sách yêu cầu rút tiền thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<WithdrawalResponse>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Xử lý duyệt hoặc từ chối yêu cầu rút tiền của shop
        /// </summary>
        [HttpPost("withdrawals/{id:int}/process")]
        public async Task<IActionResult> ProcessWithdrawal(int id, [FromBody] ProcessWithdrawalRequest request)
        {
            try
            {
                var result = await _adminService.ProcessWithdrawalAsync(id, request);
                var message = request.IsApproved
                    ? "Duyệt yêu cầu rút tiền thành công."
                    : "Đã từ chối yêu cầu rút tiền. Số tiền đã được hoàn lại số dư khả dụng của shop.";
                return Ok(ApiResponse<WithdrawalResponse>.SuccessResponse(result, message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<WithdrawalResponse>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<WithdrawalResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<WithdrawalResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy số liệu thống kê tài chính và vận hành tổng quan (GMV, Doanh thu phí ký quỹ, số đơn hàng, số shop)
        /// </summary>
        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
        {
            try
            {
                var result = await _adminService.GetAnalyticsAsync();
                return Ok(ApiResponse<AdminAnalyticsResponse>.SuccessResponse(result, "Lấy số liệu thống kê thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<AdminAnalyticsResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }
    }
}
