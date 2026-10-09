using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    [ApiController]
    [Route("api/stores/{storeId:int}/reviews")]
    public class StoreReviewsController : ControllerBase
    {
        private readonly IStoreReviewService _reviewService;

        public StoreReviewsController(IStoreReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        /// <summary>
        /// Tao review cho cua hang (Chi nguoi da mua hang va hoan thanh don hang moi duoc review).
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReview(int storeId, [FromBody] CreateStoreReviewRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le", GetModelErrors()));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var result = await _reviewService.CreateReviewAsync(userId.Value, storeId, request);
                return StatusCode(201, ApiResponse.Ok(result, "Review created successfully"));
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
        /// Cap nhat review cua hang (Chi chu nhan review moi duoc cap nhat).
        /// </summary>
        [HttpPut("{reviewId:int}")]
        [Authorize]
        public async Task<IActionResult> UpdateReview(int storeId, int reviewId, [FromBody] UpdateStoreReviewRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Du lieu khong hop le", GetModelErrors()));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var result = await _reviewService.UpdateReviewAsync(userId.Value, storeId, reviewId, request);
                return Ok(ApiResponse.Ok(result, "Review updated successfully"));
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
        /// Xem danh sach review cua cua hang (Public, phan trang, sap xep newest/highest/lowest).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetReviews(int storeId, [FromQuery] StoreReviewQuery query)
        {
            try
            {
                var result = await _reviewService.GetReviewsAsync(storeId, query);
                return Ok(ApiResponse.Ok(result, "Reviews retrieved successfully"));
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
        /// Xoa review cua hang (Chi nguoi tao review hoac Admin moi duoc xoa).
        /// </summary>
        [HttpDelete("{reviewId:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int storeId, int reviewId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le", new List<string> { "UNAUTHORIZED" }));
            }

            var isAdmin = User.IsInRole("Admin");

            try
            {
                await _reviewService.DeleteReviewAsync(userId.Value, isAdmin, storeId, reviewId);
                return Ok(ApiResponse.Ok(null!, "Review deleted successfully"));
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
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int id)) return null;
            return id;
        }

        private List<string> GetModelErrors() =>
            ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
    }
}
