using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Helpers;
using Doosii.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Doosii.API.Controllers;

[ApiController, Route("api/community"), CommunityValidation, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CommunityController(ICommunityPostService posts, ICommunityInteractionService interactions, IPostReportService reports, ILogger<CommunityController> logger) : ControllerBase
{
    private int? Viewer => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private int Actor => Viewer ?? throw new ServiceException(401, "Vui lòng đăng nhập.", "UNAUTHORIZED");
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private async Task<IActionResult> Execute<T>(Func<Task<T>> action, int status = 200)
    {
        try
        {
            if (Request.Headers.ContainsKey("Authorization") && User.Identity?.IsAuthenticated != true) throw new ServiceException(401, "Phiên đăng nhập không hợp lệ.", "UNAUTHORIZED");
            return StatusCode(status, ApiResponse.Ok((await action())!));
        }
        catch (ServiceException ex) { return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new() { ex.ErrorCode })); }
        catch (JsonException) { return BadRequest(ApiResponse.Fail("Payload JSON không hợp lệ hoặc chứa trường không hỗ trợ.", new() { "INVALID_PAYLOAD" })); }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || CommunityDatabaseErrors.IsConflict(ex)) { return Conflict(ApiResponse.Fail("Dữ liệu vừa thay đổi. Hãy thử lại.", new() { "COMMUNITY_CONFLICT" })); }
        catch (Exception ex) { logger.LogError(ex, "Community API request failed"); return StatusCode(500, ApiResponse.Fail("Không xử lý được yêu cầu.", new() { "INTERNAL_ERROR" })); }
    }
    private static CommunityPostWriteRequest Parse(CommunityPostForm form) => JsonSerializer.Deserialize<CommunityPostWriteRequest>(form.Payload, PayloadOptions)
        ?? throw new ServiceException(400, "Payload bắt buộc.", "INVALID_PAYLOAD");

    [HttpPost("posts"), Authorize, Consumes("multipart/form-data"), RequestSizeLimit(150L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 150L * 1024 * 1024)]
    public Task<IActionResult> Create([FromForm] CommunityPostForm form) => Execute(async () => await posts.CreateAsync(Actor, Parse(form), Request.Form.Files.ToList()), 201);
    [HttpPut("posts/{id:int}"), Authorize, Consumes("multipart/form-data"), RequestSizeLimit(150L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 150L * 1024 * 1024)]
    public Task<IActionResult> Update(int id, [FromForm] CommunityPostForm form) => Execute(async () => await posts.UpdateAsync(id, Actor, Parse(form), Request.Form.Files.ToList()));
    [HttpGet("posts/{id:int}"), AllowAnonymous]
    public Task<IActionResult> Detail(int id) => Execute(() => posts.DetailAsync(id, Viewer));
    [HttpGet("posts"), AllowAnonymous]
    public Task<IActionResult> Feed([FromQuery] CommunityPostQuery query) => Execute(() => posts.FeedAsync(query, Viewer));
    [HttpPatch("posts/{id:int}/visibility"), Authorize]
    public Task<IActionResult> Visibility(int id, [FromBody] CommunityVisibilityRequest request) => Execute(() => posts.VisibilityAsync(id, Actor, request));
    [HttpPatch("posts/{id:int}/sale-status"), Authorize]
    public Task<IActionResult> Sale(int id, [FromBody] CommunitySaleRequest request) => Execute(() => posts.SaleAsync(id, Actor, request));
    [HttpPut("posts/{id:int}/reaction"), Authorize]
    public Task<IActionResult> Like(int id) => Execute(() => interactions.SetLikeAsync("", id, Actor, true));
    [HttpDelete("posts/{id:int}/reaction"), Authorize]
    public Task<IActionResult> Unlike(int id) => Execute(() => interactions.SetLikeAsync("", id, Actor, false));
    [HttpGet("posts/{id:int}/comments"), AllowAnonymous]
    public Task<IActionResult> Comments(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? parentId = null) => Execute(async () =>
    {
        var result = await interactions.CommentsAsync("", id, page, pageSize, parentId);
        return new CommunityPage<CommunityCommentDto> { Items = result.Items, TotalItems = result.TotalCount, Page = result.Page, PageSize = result.PageSize };
    });
    [HttpPost("posts/{id:int}/comments"), Authorize]
    public Task<IActionResult> Comment(int id, [FromBody] CommunityCommentRequest request) => Execute(() => interactions.CommentAsync("", id, Actor, request), 201);
    [HttpDelete("posts/{id:int}/comments/{commentId:int}"), Authorize]
    public Task<IActionResult> DeleteComment(int id, int commentId) => Execute(async () => { await interactions.DeleteCommentAsync("", id, commentId, Actor); return true; });
    [HttpPost("posts/{id:int}/reports"), Authorize]
    public Task<IActionResult> Report(int id, [FromBody] CommunityReportRequest request) => Execute(() => reports.CreateReportAsync(id, Actor, new() { Reason = request.Reason, Description = request.Description, EvidenceUrls = request.EvidenceUrls }), 201);
    [HttpGet("moderation/reports"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Queue([FromQuery] PostReportQuery query) => Execute(async () =>
    {
        await interactions.EnsureAdminAsync(Actor); var result = await reports.GetReportsQueueAsync(query);
        return new CommunityPage<PostReportResponseDto> { Items = result.Items, TotalItems = result.TotalCount, Page = result.Page, PageSize = result.PageSize };
    });
    [HttpPut("moderation/reports/{id:int}/resolve"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Resolve(int id, [FromBody] ResolvePostReportRequest request) => Execute(() => reports.ResolveReportAsync(id, Actor, request));
    [HttpGet("moderation/reports/{id:int}/post"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Moderation(int id) => Execute(async () =>
    {
        var report = await interactions.ModerationPostAsync(id, Actor);
        return await posts.DetailAsync(report.PostId, Actor, true);
    });
}
