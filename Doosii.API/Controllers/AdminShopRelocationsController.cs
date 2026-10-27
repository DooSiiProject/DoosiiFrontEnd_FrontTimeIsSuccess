using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Helpers;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers;

[ApiController, Authorize, CommunityValidation]
[Route("api/admin/shop-relocations")]
public class AdminShopRelocationsController(IShopRelocationService service, ILogger<AdminShopRelocationsController> logger) : ControllerBase
{
    private async Task<IActionResult> Run<T>(Func<int, Task<T>> action) where T : notnull
    {
        Response.Headers.CacheControl = "no-store";
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized(ApiResponse.Fail("Phiên không hợp lệ."));
        try { return Ok(ApiResponse.Ok(await action(id))); }
        catch (ServiceException ex) { return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (Exception ex) when (CommunityDatabaseErrors.IsConflict(ex)) { return Conflict(ApiResponse.Fail("Yêu cầu vừa thay đổi hoặc mã gửi đã được dùng. Hãy tải lại kết quả trước khi thử lại.")); }
        catch (Exception ex) { logger.LogError(ex, "Admin relocation action failed"); return StatusCode(500, ApiResponse.Fail("Chưa thể tải hoặc lưu quyết định. Hãy tải lại để kiểm tra kết quả.")); }
    }
    [HttpGet]
    public Task<IActionResult> List([FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10) => Run(id => service.AdminListAsync(id, status, page, pageSize));
    [HttpGet("{requestId:int}")]
    public Task<IActionResult> Detail(int requestId) => Run(id => service.AdminDetailAsync(id, requestId));
    [HttpPost("{requestId:int}/review")]
    public Task<IActionResult> Review(int requestId, [FromBody] ReviewShopRelocationRequest request) => Run(id => service.ReviewAsync(id, requestId, request));
}
