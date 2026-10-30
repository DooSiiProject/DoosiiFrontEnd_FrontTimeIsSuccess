using System.Security.Claims;
using Doosii.BLL.Common;
using Doosii.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Doosii.API.Controllers;

[ApiController, Authorize, Route("api/returns")]
public class ReturnsController(ReturnService service) : ControllerBase
{
    private int Actor => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private async Task<IActionResult> Execute(Func<Task<object?>> operation)
    {
        if (Actor == 0) return Unauthorized();
        try { return Ok(ApiResponse<object?>.SuccessResponse(await operation())); }
        catch (KeyNotFoundException e) { return NotFound(ApiResponse<object>.ErrorResponse(e.Message)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.ErrorResponse("Bạn không có quyền thực hiện thao tác.")); }
        catch (InvalidOperationException e) { return Conflict(ApiResponse<object>.ErrorResponse(e.Message)); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { return Conflict(ApiResponse<object>.ErrorResponse("Yêu cầu đã thay đổi, vui lòng tải lại.")); }
    }
    [HttpGet("{orderId:int}")]
    public Task<IActionResult> Get(int orderId) => Execute(() => service.Get(orderId, Actor));
    [HttpGet("admin/pending"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Pending() => Execute(async () => await service.ListAdmin(Actor));
    [HttpPost("{orderId:int}")]
    public Task<IActionResult> Create(int orderId, ReturnInput input) => Execute(async () => await service.Create(orderId, Actor, input));
    [HttpPost("{orderId:int}/{actionName}")]
    public Task<IActionResult> Change(int orderId, string actionName, ReturnInput input) => Execute(async () => await service.Change(orderId, Actor, actionName, input));
}
