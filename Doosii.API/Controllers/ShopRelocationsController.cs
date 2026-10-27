using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Helpers;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers;

[ApiController, Authorize, CommunityValidation]
[Route("api/stores/{storeId:int}/relocation-requests")]
public class ShopRelocationsController(IShopRelocationService service, ILogger<ShopRelocationsController> logger) : ControllerBase
{
    private async Task<IActionResult> Run<T>(Func<int, Task<T>> action) where T : notnull
    {
        Response.Headers.CacheControl = "no-store";
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized(ApiResponse.Fail("Phiên không hợp lệ."));
        try { return Ok(ApiResponse.Ok(await action(userId))); }
        catch (ServiceException ex) { return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode })); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (Exception ex) when (CommunityDatabaseErrors.IsConflict(ex)) { return Conflict(ApiResponse.Fail("Dữ liệu vừa thay đổi. Hãy tải lại lịch sử trước khi thử lại.")); }
        catch (Exception ex) { logger.LogError(ex, "Shop relocation request failed"); return StatusCode(500, ApiResponse.Fail("Chưa thể tải hoặc lưu yêu cầu. Hãy tải lại lịch sử để kiểm tra kết quả.")); }
    }
    [HttpGet]
    public Task<IActionResult> List(int storeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10) => Run(id => service.GetAsync(id, storeId, page, pageSize));
    [HttpPost]
    public Task<IActionResult> Create(int storeId, [FromBody] CreateShopRelocationRequest request) => Run(id => service.CreateAsync(id, storeId, request));
    [HttpPost("{requestId:int}/cancel")]
    public Task<IActionResult> Cancel(int storeId, int requestId) => Run(id => service.CancelAsync(id, storeId, requestId));
}
