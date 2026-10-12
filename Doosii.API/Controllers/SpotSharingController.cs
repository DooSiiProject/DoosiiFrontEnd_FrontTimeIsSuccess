using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    /// <summary>
    /// Endpoint tao va xem bai Spot Sharing Post.
    /// POST /api/forum/posts/spot    - Tao bai (can dang nhap)
    /// GET  /api/forum/posts/spot/{id} - Xem chi tiet (public)
    /// </summary>
    [ApiController]
    [Route("api/forum/posts/spot")]
    public class SpotSharingController : ControllerBase
    {
        private readonly ISpotSharingService _spotService;

        public SpotSharingController(ISpotSharingService spotService)
        {
            _spotService = spotService;
        }

        /// <summary>
        /// Tao moi bai Spot Sharing Post.
        /// Bat buoc dang nhap. Nhan multipart/form-data.
        /// Can co TaggedStoreId hoac cap (Latitude, Longitude).
        /// </summary>
        [HttpPost]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateSpotPost([FromForm] CreateSpotPostRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le.", GetModelErrors()));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Token khong hop le hoac nguoi dung chua dang nhap.",
                    new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var result = await _spotService.CreateSpotPostAsync(userId.Value, request);
                return StatusCode(201, ApiResponse.Ok(result, "Spot sharing post created successfully"));
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
        /// Xem chi tiet bai Spot Sharing theo ID.
        /// Endpoint public, khong yeu cau dang nhap.
        /// Tra 404 neu bai khong ton tai hoac dang o trang thai HIDDEN.
        /// </summary>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSpotPostById(int id)
        {
            try
            {
                var result = await _spotService.GetSpotPostByIdAsync(id);
                if (result == null)
                {
                    return NotFound(ApiResponse.Fail(
                        "Khong tim thay bai dang hoac bai dang da bi an.",
                        new List<string> { "POST_NOT_FOUND" }));
                }

                return Ok(ApiResponse.Ok(result, "Lay chi tiet bai spot thanh cong."));
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
            if (int.TryParse(claim, out int userId)) return userId;
            return null;
        }

        private List<string> GetModelErrors() =>
            ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
    }

    /// <summary>
    /// Endpoint lay danh sach Spot Posts theo Store.
    /// GET /api/stores/{storeId}/spot-posts - Public, co pagination.
    /// </summary>
    [ApiController]
    [Route("api/stores/{storeId:int}/spot-posts")]
    public class StoreSpotPostsController : ControllerBase
    {
        private readonly ISpotSharingService _spotService;

        public StoreSpotPostsController(ISpotSharingService spotService)
        {
            _spotService = spotService;
        }

        /// <summary>
        /// Lay danh sach Spot Posts cua mot Store co phan trang.
        /// Moi nhat truoc, chi lay bai co Status = ACTIVE.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetSpotPostsByStore(
            int storeId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            try
            {
                var result = await _spotService.GetSpotPostsByStoreAsync(storeId, page, pageSize);
                return Ok(ApiResponse.Ok(result, "Lay danh sach spot posts thanh cong."));
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
    }
}
