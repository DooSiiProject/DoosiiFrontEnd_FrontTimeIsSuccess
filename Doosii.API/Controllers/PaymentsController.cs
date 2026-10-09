using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.Common;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
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
        /// Tạo mã VietQR động PayOS để thanh toán đơn hàng (Chỉ Người mua)
        /// </summary>
        [HttpPost("payos-qr")]
        [Authorize]
        public async Task<IActionResult> CreatePaymentQr([FromBody] CreatePaymentQrRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<PaymentQrResponse>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh."));
            }

            try
            {
                var result = await _paymentService.CreatePaymentQrAsync(request.OrderId, userId.Value);
                return Ok(ApiResponse<PaymentQrResponse>.SuccessResponse(result, "Tạo thông tin thanh toán VietQR thành công. Vui lòng quét mã để chuyển khoản."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<PaymentQrResponse>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<PaymentQrResponse>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<PaymentQrResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<PaymentQrResponse>.ErrorResponse("Đã xảy ra lỗi khi tạo mã thanh toán VietQR.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Kiểm tra trạng thái thanh toán hiện tại của đơn hàng
        /// </summary>
        [HttpGet("order/{orderId:int}/status")]
        [Authorize]
        public async Task<IActionResult> GetPaymentStatus(int orderId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<PaymentQrResponse>.ErrorResponse("Token không hợp lệ."));
            }

            try
            {
                var result = await _paymentService.GetPaymentStatusAsync(orderId, userId.Value);
                return Ok(ApiResponse<PaymentQrResponse>.SuccessResponse(result, "Lấy trạng thái thanh toán thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<PaymentQrResponse>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<PaymentQrResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<PaymentQrResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Tiếp nhận Webhook callback từ cổng PayOS khi người mua chuyển khoản thành công
        /// </summary>
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> ReceivePayOsWebhook([FromBody] PayOsWebhookPayload payload)
        {
            try
            {
                var rawBody = JsonSerializer.Serialize(payload);
                var success = await _paymentService.ProcessPayOsWebhookAsync(payload, rawBody);

                if (!success)
                {
                    return BadRequest(new { success = false, message = "Xác thực webhook thất bại hoặc không tìm thấy đơn hàng." });
                }

                return Ok(new { success = true, message = "Webhook đã được xử lý thành công. Đơn hàng chuyển sang trạng thái tạm giữ (ESCROW_HOLDING)." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi xử lý webhook.", error = ex.Message });
            }
        }
    }
}
