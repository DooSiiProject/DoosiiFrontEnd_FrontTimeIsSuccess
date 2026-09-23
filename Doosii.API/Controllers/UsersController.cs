using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Cap nhat thong tin ca nhan cua nguoi dung dang dang nhap
        /// </summary>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le.", errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le hoac thieu thong tin nguoi dung."));
            }

            try
            {
                var result = await _userService.UpdateProfileAsync(userId.Value, request);
                return Ok(ApiResponse.Ok(result, "Cap nhat profile thanh cong."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        /// <summary>
        /// Nop ho so dang ky Seller (KYC). Chi Customer moi duoc nop.
        /// </summary>
        [HttpPost("seller-application")]
        public async Task<IActionResult> SubmitSellerApplication([FromBody] SellerApplicationRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le.", errors));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le hoac thieu thong tin nguoi dung."));
            }

            try
            {
                var result = await _userService.SubmitSellerApplicationAsync(userId.Value, request);
                return StatusCode(201, ApiResponse.Ok(result, "Nop ho so KYC thanh cong. Vui long cho Admin duyet."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int id))
                return null;
            return id;
        }
    }
}