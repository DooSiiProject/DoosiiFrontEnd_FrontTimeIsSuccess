using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.Common;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/seller/wallet")]
    [Authorize]
    public class SellerWalletController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public SellerWalletController(IWalletService walletService)
        {
            _walletService = walletService;
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
        /// Xem thông tin ví người bán (Số dư khả dụng, tiền ký quỹ, tổng doanh thu và lịch sử giao dịch)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetWallet()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<WalletResponse>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _walletService.GetSellerWalletAsync(userId.Value);
                return Ok(ApiResponse<WalletResponse>.SuccessResponse(result, "Lấy thông tin ví thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<WalletResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<WalletResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Gửi yêu cầu rút tiền về tài khoản ngân hàng cá nhân (Tối thiểu 50,000 VND)
        /// </summary>
        [HttpPost("withdraw")]
        public async Task<IActionResult> RequestWithdrawal([FromBody] CreateWithdrawalRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<WithdrawalResponse>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _walletService.CreateWithdrawalRequestAsync(userId.Value, request);
                return Ok(ApiResponse<WithdrawalResponse>.SuccessResponse(result, "Gửi yêu cầu rút tiền thành công. Yêu cầu đang chờ quản trị viên phê duyệt."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<WithdrawalResponse>.ErrorResponse(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<WithdrawalResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<WithdrawalResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy danh sách lịch sử các yêu cầu rút tiền của người dùng
        /// </summary>
        [HttpGet("withdrawals")]
        public async Task<IActionResult> GetMyWithdrawals()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<List<WithdrawalResponse>>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _walletService.GetMyWithdrawalRequestsAsync(userId.Value);
                return Ok(ApiResponse<List<WithdrawalResponse>>.SuccessResponse(result, "Lấy danh sách yêu cầu rút tiền thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<WithdrawalResponse>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }
    }
}
