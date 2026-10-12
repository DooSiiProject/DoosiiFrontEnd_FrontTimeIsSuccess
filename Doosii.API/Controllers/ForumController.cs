using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/forum")]
    public class ForumController : ControllerBase
    {
        private readonly IPassPostService _passPostService;

        public ForumController(IPassPostService passPostService)
        {
            _passPostService = passPostService;
        }

        /// <summary>
        /// Tao moi bai PassPost tren dien dan voi anh (1-5 anh) qua multipart/form-data.
        /// Yeu cau dang nhap (Customer hoac Seller deu duoc phep).
        /// </summary>
        [HttpPost("posts/pass")]
        [HttpPost("pass-posts")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreatePassPost([FromForm] CreatePassPostRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le.", GetModelErrors()));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le hoac nguoi dung chua dang nhap.", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var result = await _passPostService.CreatePassPostAsync(userId.Value, request);
                return StatusCode(201, ApiResponse.Ok(result, "Pass post created successfully"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Xem chi tiet bai PassPost theo ID.
        /// Endpoint public, khong yeu cau dang nhap.
        /// Tra ve 404 neu khong tim thay hoac bai dang bi an (HIDDEN).
        /// </summary>
        [HttpGet("posts/{id:int}")]
        [HttpGet("pass-posts/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPassPostById(int id)
        {
            try
            {
                var result = await _passPostService.GetPassPostByIdAsync(id);
                if (result == null)
                {
                    return NotFound(ApiResponse.Fail("Khong tim thay bai dang hoac bai dang da bi an.", new List<string> { "POST_NOT_FOUND" }));
                }

                return Ok(ApiResponse.Ok(result, "Lay chi tiet bai pass thanh cong."));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail("An unexpected error occurred", new List<string> { ex.Message }));
            }
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

        private List<string> GetModelErrors() =>
            ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
    }
}
