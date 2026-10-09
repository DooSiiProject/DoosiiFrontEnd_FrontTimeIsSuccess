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
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
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
        /// Tạo đơn hàng ký quỹ (Escrow Order) và khóa sản phẩm trong 15 phút
        /// </summary>
        [HttpPost("create-escrow")]
        public async Task<IActionResult> CreateEscrowOrder([FromBody] CreateEscrowOrderRequest request)
        {
            var buyerId = GetCurrentUserId();
            if (buyerId == null)
            {
                return Unauthorized(ApiResponse<OrderResponse>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _orderService.CreateEscrowOrderAsync(buyerId.Value, request);
                return Ok(ApiResponse<OrderResponse>.SuccessResponse(result, "Tạo đơn hàng ký quỹ thành công. Sản phẩm được giữ chỗ trong 15 phút để thanh toán."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<OrderResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ khi tạo đơn hàng.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy chi tiết đơn hàng theo ID (Chỉ người mua hoặc người bán đơn hàng)
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<OrderResponse>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _orderService.GetOrderByIdAsync(id, userId.Value);
                return Ok(ApiResponse<OrderResponse>.SuccessResponse(result, "Lấy thông tin đơn hàng thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<OrderResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy danh sách các đơn hàng người dùng đã mua
        /// </summary>
        [HttpGet("my-purchases")]
        public async Task<IActionResult> GetMyPurchases()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<List<OrderResponse>>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _orderService.GetMyPurchasesAsync(userId.Value);
                return Ok(ApiResponse<List<OrderResponse>>.SuccessResponse(result, "Lấy danh sách đơn hàng đã mua thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<OrderResponse>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy danh sách các đơn hàng người dùng đã/đang bán
        /// </summary>
        [HttpGet("my-sales")]
        public async Task<IActionResult> GetMySales()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<List<OrderResponse>>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _orderService.GetMySalesAsync(userId.Value);
                return Ok(ApiResponse<List<OrderResponse>>.SuccessResponse(result, "Lấy danh sách đơn bán thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<OrderResponse>>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Cập nhật thông tin giao hàng & mã vận đơn (Dành cho Người bán, chuyển sang IN_TRANSIT)
        /// </summary>
        [HttpPost("{id:int}/ship")]
        public async Task<IActionResult> ShipOrder(int id, [FromBody] ShipOrderRequest request)
        {
            var sellerId = GetCurrentUserId();
            if (sellerId == null)
            {
                return Unauthorized(ApiResponse<OrderResponse>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _orderService.ShipOrderAsync(id, sellerId.Value, request);
                return Ok(ApiResponse<OrderResponse>.SuccessResponse(result, "Cập nhật gửi hàng thành công. Đơn hàng hiện đang được vận chuyển."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<OrderResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Người mua xác nhận đã nhận hàng & hài lòng (Chuyển sang COMPLETED_RELEASED và giải ngân ký quỹ)
        /// </summary>
        [HttpPost("{id:int}/confirm-received")]
        public async Task<IActionResult> ConfirmOrderReceived(int id)
        {
            var buyerId = GetCurrentUserId();
            if (buyerId == null)
            {
                return Unauthorized(ApiResponse<OrderResponse>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _orderService.ConfirmOrderReceivedAsync(id, buyerId.Value);
                return Ok(ApiResponse<OrderResponse>.SuccessResponse(result, "Xác nhận nhận hàng thành công. Tiền ký quỹ đã được giải ngân cho người bán."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<OrderResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<OrderResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Người mua mở khiếu nại đơn hàng (trong vòng 24h, kèm video unboxing hoặc tối thiểu 2 ảnh lỗi)
        /// </summary>
        [HttpPost("{id:int}/dispute")]
        public async Task<IActionResult> CreateDispute(int id, [FromBody] CreateDisputeRequest request)
        {
            var buyerId = GetCurrentUserId();
            if (buyerId == null)
            {
                return Unauthorized(ApiResponse<DisputeResponse>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _orderService.CreateDisputeAsync(id, buyerId.Value, request);
                return Ok(ApiResponse<DisputeResponse>.SuccessResponse(result, "Mở khiếu nại thành công. Đơn hàng và tiền ký quỹ đã được đóng băng để Admin phân xử."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<DisputeResponse>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<DisputeResponse>.ErrorResponse(ex.Message));
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
        /// Xem thông tin khiếu nại của đơn hàng (Chỉ Người mua, Người bán hoặc Admin)
        /// </summary>
        [HttpGet("{id:int}/dispute")]
        public async Task<IActionResult> GetOrderDispute(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<DisputeResponse>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin định danh người dùng."));
            }

            try
            {
                var result = await _orderService.GetOrderDisputeAsync(id, userId.Value);
                if (result == null)
                {
                    return NotFound(ApiResponse<DisputeResponse>.ErrorResponse("Đơn hàng này chưa có khiếu nại nào."));
                }
                return Ok(ApiResponse<DisputeResponse>.SuccessResponse(result, "Lấy thông tin khiếu nại thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<DisputeResponse>.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<DisputeResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<DisputeResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }
    }
}
