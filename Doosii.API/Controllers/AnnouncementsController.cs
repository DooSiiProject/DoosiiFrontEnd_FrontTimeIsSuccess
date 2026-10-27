using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.BLL.Exceptions;
namespace Doosii.API.Controllers;
[ApiController, CommunityValidation]
public class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcements;
    public AnnouncementsController(IAnnouncementService announcements) { _announcements = announcements; }
    private async Task<IActionResult> Run<T>(Func<Task<T>> action) where T : notnull
    {
        try { return Ok(ApiResponse.Ok(await action())); }
        catch (ServiceException ex) { return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (Exception ex) when (Doosii.BLL.Helpers.CommunityDatabaseErrors.IsConflict(ex)) { return Conflict(ApiResponse.Fail("Dữ liệu vừa thay đổi. Hãy tải lại và kiểm tra lịch sử.")); }
        catch (Exception) { return StatusCode(500, ApiResponse.Fail("Chưa thể tải hoặc lưu sự kiện. Hãy tải lại lịch sử để kiểm tra kết quả.")); }
    }
    private Task<IActionResult> Private<T>(Func<int, Task<T>> action) where T : notnull => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? Run(() => action(id)) : Task.FromResult<IActionResult>(Unauthorized(ApiResponse.Fail("Phiên không hợp lệ.")));
    [Authorize, HttpPost("api/seller/announcements")] public Task<IActionResult> Create([FromBody] CreateAnnouncementRequest request) => Private(id => _announcements.CreateAnnouncementAsync(id, request));
    [Authorize, HttpGet("api/seller/announcements")] public Task<IActionResult> Mine() => Private(_announcements.GetSellerAnnouncementsAsync);
    [Authorize, HttpGet("api/seller/announcements/options")] public Task<IActionResult> Options() => Private(_announcements.GetOptionsAsync);
    [AllowAnonymous, HttpGet("api/announcements/upcoming")] public Task<IActionResult> Upcoming([FromQuery] int? storeId) => Run(() => _announcements.GetUpcomingAnnouncementsAsync(storeId));
}
