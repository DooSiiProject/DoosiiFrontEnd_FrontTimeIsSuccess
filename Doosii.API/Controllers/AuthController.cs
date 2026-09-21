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
        /// Làm mới Access Token bằng Refresh Token
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request.RefreshToken);
                return Ok(ApiResponse<AuthResponse>.SuccessResponse(result, "Làm mới token thành công."));
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
        /// Gửi mã OTP 6 số để khôi phục mật khẩu qua Email
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                await _authService.ForgotPasswordAsync(request.Email);
                return Ok(ApiResponse<string>.SuccessResponse("Mã OTP đã được gửi đến email của bạn nếu tài khoản tồn tại."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Xác thực mã OTP 6 số
        /// </summary>
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                var isValid = await _authService.VerifyOtpAsync(request);
                return Ok(ApiResponse<bool>.SuccessResponse(isValid, "Mã OTP hợp lệ."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Đặt lại mật khẩu mới bằng mã OTP
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                await _authService.ResetPasswordAsync(request);
                return Ok(ApiResponse<string>.SuccessResponse("Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<string>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.ErrorResponse("Đã xảy ra lỗi máy chủ nội bộ.", new List<string> { ex.Message }));
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
