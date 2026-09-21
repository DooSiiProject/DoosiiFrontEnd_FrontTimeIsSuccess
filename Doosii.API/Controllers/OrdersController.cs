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
    }
}
