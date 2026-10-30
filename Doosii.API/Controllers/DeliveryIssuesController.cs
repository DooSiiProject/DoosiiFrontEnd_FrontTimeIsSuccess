using System.Security.Claims;
using Doosii.BLL.Common;
using Doosii.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Doosii.API.Controllers;
[ApiController, Authorize, Route("api/orders/{orderId:int}/delivery-issue")]
public class DeliveryIssuesController(DeliveryIssueService service) : ControllerBase
{
    private int Actor => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private async Task<IActionResult> Run(Func<Task<object?>> work)
    {
        if (Actor == 0) return Unauthorized();
        try { return Ok(ApiResponse<object?>.SuccessResponse(await work())); }
        catch (KeyNotFoundException e) { return NotFound(ApiResponse<object>.ErrorResponse(e.Message)); }
        catch (UnauthorizedAccessException) { return StatusCode(403, ApiResponse<object>.ErrorResponse("Bạn không có quyền thực hiện thao tác.")); }
        catch (InvalidOperationException e) { return Conflict(ApiResponse<object>.ErrorResponse(e.Message)); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { return Conflict(ApiResponse<object>.ErrorResponse("Dữ liệu đã thay đổi. Hãy tải lại.")); }
        catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 1205) { return Conflict(ApiResponse<object>.ErrorResponse("Dữ liệu đang được cập nhật. Hãy thử lại.")); }
    }
    [HttpGet] public Task<IActionResult> Get(int orderId) => Run(() => service.Get(orderId, Actor));
    [HttpPost("{actionName}")] public Task<IActionResult> Change(int orderId, string actionName, DeliveryIssueInput input) => Run(async () => await service.Change(orderId, Actor, actionName, input));
}
