using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.Helpers;
using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers;

[CommunityValidation]
    [ApiController, Route("api/forum"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[LegacyCommunityUnavailable]
    public class CommunityInteractionsController(ICommunityInteractionService service, IPostReportService reports, ILogger<CommunityInteractionsController> logger) : ControllerBase
{
    private int? Viewer => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private int Actor => Viewer ?? throw new ServiceException(401, "Vui long dang nhap.", "UNAUTHORIZED");
    private async Task<IActionResult> Execute<T>(Func<Task<T>> run, int status = 200) where T : notnull
    {
        try { return StatusCode(status, ApiResponse.Ok(await run(), "Thanh cong.")); }
        catch (ServiceException ex) { return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode })); }
        catch (Exception ex) when (CommunityDatabaseErrors.IsConflict(ex))
        { return Conflict(ApiResponse.Fail("Du lieu vua thay doi. Vui long tai lai.", new List<string> { "COMMUNITY_CONFLICT" })); }
        catch (Exception ex) { logger.LogError(ex, "Community request failed"); return StatusCode(500, ApiResponse.Fail("Khong xu ly duoc yeu cau. Vui long thu lai.")); }
    }
    [HttpGet("interaction-summaries"), AllowAnonymous]
    public Task<IActionResult> Summaries([FromQuery] string[] keys) => Execute(async () =>
    {
        if (Request.Headers.ContainsKey("Authorization") && User.Identity?.IsAuthenticated != true)
            throw new ServiceException(401, "Phien dang nhap het han.", "UNAUTHORIZED");
        return await service.SummariesAsync(keys, Viewer);
    });
    [HttpPut("posts/{postType}/{postId:int}/reactions"), Authorize]
    public Task<IActionResult> Like(string postType, int postId) => Execute(() => service.SetLikeAsync(postType, postId, Actor, true));
    [HttpDelete("posts/{postType}/{postId:int}/reactions"), Authorize]
    public Task<IActionResult> Unlike(string postType, int postId) => Execute(() => service.SetLikeAsync(postType, postId, Actor, false));
    [HttpGet("posts/{postType}/{postId:int}/comments"), AllowAnonymous]
    public Task<IActionResult> Comments(string postType, int postId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? parentId = null)
        => Execute(() => service.CommentsAsync(postType, postId, page, pageSize, parentId));
    [HttpPost("posts/{postType}/{postId:int}/comments"), Authorize]
    public Task<IActionResult> Comment(string postType, int postId, [FromBody] CommunityCommentRequest request)
        => Execute(() => service.CommentAsync(postType, postId, Actor, request), 201);
    [HttpDelete("posts/{postType}/{postId:int}/comments/{commentId:int}"), Authorize]
    public Task<IActionResult> DeleteComment(string postType, int postId, int commentId) => Execute(async () =>
    { await service.DeleteCommentAsync(postType, postId, commentId, Actor); return true; });
    [HttpPost("posts/{postType}/{postId:int}/reports"), Authorize]
    public Task<IActionResult> Report(string postType, int postId, [FromBody] CommunityReportRequest request) => Execute(() => reports.CreateReportAsync(postId, Actor,
        new CreatePostReportRequest { PostType = postType, Reason = request.Reason, Description = request.Description }), 201);
    [HttpGet("moderation/reports"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Queue([FromQuery] PostReportQuery query) => Execute(async () =>
    { await service.EnsureAdminAsync(Actor); return await reports.GetReportsQueueAsync(query); });
    [HttpPut("moderation/reports/{reportId:int}/resolve"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Resolve(int reportId, [FromBody] ResolvePostReportRequest request) => Execute(() => reports.ResolveReportAsync(reportId, Actor, request));
    [HttpGet("moderation/reports/{reportId:int}/post"), Authorize(Roles = "Admin")]
    public Task<IActionResult> ModerationPost(int reportId) => Execute(() => service.ModerationPostAsync(reportId, Actor));
}

