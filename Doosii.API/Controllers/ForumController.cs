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
        private readonly ICommunityFeedService _feedService;
        private readonly ICommentService _commentService;
        private readonly IReactionService _reactionService;
        private readonly IPostReportService _postReportService;

        public ForumController(
            IPassPostService passPostService,
            ICommunityFeedService feedService,
            ICommentService commentService,
            IReactionService reactionService,
            IPostReportService postReportService)
        {
            _passPostService = passPostService;
            _feedService = feedService;
            _commentService = commentService;
            _reactionService = reactionService;
            _postReportService = postReportService;
        }

        // =========================================================================
        // COMMUNITY FEED
        // =========================================================================

        /// <summary>
        /// Lay danh sach Community Feed (tong hop PassPost va SpotSharingPost).
        /// Endpoint public, khong bat buoc dang nhap.
        /// Query parameters:
        /// - tab: newest | near-you | by-style (mac dinh: newest)
        /// - style: bat buoc khi tab=by-style
        /// - lat, lng: bat buoc khi tab=near-you (-90..90, -180..180)
        /// - radiusKm: 1, 3, 5 hoac 10 (mac dinh: 5)
        /// - page: bat dau tu 1
        /// - pageSize: mac dinh 20, toi da 50
        /// </summary>
        [HttpGet("feed")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCommunityFeed([FromQuery] CommunityFeedQuery query)
        {
            try
            {
                var result = await _feedService.GetFeedAsync(query);
                return Ok(ApiResponse.Ok(result, "Community feed retrieved successfully"));
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

        // =========================================================================
        // PASS POSTS
        // =========================================================================

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

        // =========================================================================
        // COMMENTS
        // =========================================================================

        /// <summary>
        /// Tao binh luan moi tren bai viet (ho tro comment goc hoac reply cap 1).
        /// Bat buoc dang nhap.
        /// </summary>
        [HttpPost("posts/{postId:int}/comments")]
        [Authorize]
        public async Task<IActionResult> CreateComment(int postId, [FromBody] CreateCommentRequest request)
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
                var result = await _commentService.CreateCommentAsync(postId, userId.Value, request);
                return StatusCode(201, ApiResponse.Ok(result, "Comment created successfully"));
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
        /// Lay danh sach binh luan cua mot bai viet theo dang cay phan cap (Root comments -> Replies).
        /// Endpoint public.
        /// </summary>
        [HttpGet("posts/{postId:int}/comments")]
        [AllowAnonymous]
        public async Task<IActionResult> GetComments(int postId)
        {
            try
            {
                var result = await _commentService.GetCommentsByPostIdAsync(postId);
                return Ok(ApiResponse.Ok(result, "Lay danh sach binh luan thanh cong."));
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
        /// Xoa mem binh luan (IsDeleted = true).
        /// Chi Author hoac Admin duoc phep xoa.
        /// </summary>
        [HttpDelete("comments/{commentId:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le hoac nguoi dung chua dang nhap.", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                await _commentService.DeleteCommentAsync(commentId, userId.Value);
                return Ok(ApiResponse.Ok(new { }, "Binh luan da duoc xoa thanh cong."));
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

        // =========================================================================
        // REACTIONS
        // =========================================================================

        /// <summary>
        /// Tha cam xuc hoac cap nhat / huy cam xuc tren bai viet (Toggle reaction).
        /// Request: ReactionType: LIKE hoac HEART.
        /// Bat buoc dang nhap.
        /// </summary>
        [HttpPost("posts/{postId:int}/reactions")]
        [Authorize]
        public async Task<IActionResult> ToggleReaction(int postId, [FromBody] CreateReactionRequest request)
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
                var result = await _reactionService.ToggleReactionAsync(postId, userId.Value, request);
                return Ok(ApiResponse.Ok(result, "Reaction processed successfully"));
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
        /// Lay thong tin tong so Like va Heart tren bai viet.
        /// Endpoint public. Neu da dang nhap, tra kem Reaction cua current user.
        /// </summary>
        [HttpGet("posts/{postId:int}/reactions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetReactions(int postId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _reactionService.GetReactionsSummaryAsync(postId, userId);
                return Ok(ApiResponse.Ok(result, "Lay thong tin reaction thanh cong."));
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

        // =========================================================================
        // POST REPORTS (FLAGGING)
        // =========================================================================

        /// <summary>
        /// Gui bao cao vi pham cho bai dang (PassPost hoac SpotSharingPost).
        /// Bat buoc dang nhap.
        /// POST /api/forum/posts/{postId}/report
        /// </summary>
        [HttpPost("posts/{postId:int}/report")]
        [Authorize]
        public async Task<IActionResult> ReportPost(int postId, [FromBody] CreatePostReportRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Invalid request data", GetModelErrors()));
            }

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("User is not authenticated", new List<string> { "UNAUTHORIZED" }));
            }

            try
            {
                var result = await _postReportService.CreateReportAsync(postId, userId.Value, request);
                return Ok(ApiResponse.Ok(result, "Report submitted successfully"));
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

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================

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
