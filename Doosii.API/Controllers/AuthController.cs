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
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Đăng ký tài khoản người dùng mới
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);
                return Ok(ApiResponse<UserDto>.SuccessResponse(result, "Đăng ký tài khoản thành công."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<UserDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<UserDto>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Đăng nhập hệ thống và lấy JWT Access Token
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(ApiResponse<AuthResponse>.SuccessResponse(result, "Đăng nhập thành công."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<AuthResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<AuthResponse>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lấy thông tin tài khoản đang đăng nhập (Yêu cầu JWT Bearer Token)
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(ApiResponse<UserDto>.ErrorResponse("Token không hợp lệ hoặc thiếu thông tin người dùng."));
            }

            try
            {
                var user = await _authService.GetCurrentUserAsync(userId);
                return Ok(ApiResponse<UserDto>.SuccessResponse(user, "Lấy thông tin người dùng thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<UserDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<UserDto>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }
    }
}
