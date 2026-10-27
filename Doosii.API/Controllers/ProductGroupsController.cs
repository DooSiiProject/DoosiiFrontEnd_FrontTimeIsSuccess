using System.Security.Claims;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Helpers;
using Doosii.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Doosii.API.Controllers;

[ApiController, Authorize, CommunityValidation]
[Route("api/product-groups")]
public sealed class ProductGroupsController(IProductGroupService service, ILogger<ProductGroupsController> logger) : ControllerBase
{
    private async Task<IActionResult> Run<T>(Func<int, Task<T>> action, bool anonymous = false, bool create = false) where T : notnull
    {
        Response.Headers.CacheControl = "no-store";
        var valid = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
        if (!anonymous && !valid) return Unauthorized(ApiResponse.Fail("Phiên không hợp lệ."));
        try
        {
            var data = await action(id);
            if (create && data is ProductGroupWriteResult { Replayed: false } result)
                return Created($"/api/product-groups/{result.Group.Id}", ApiResponse.Ok(data));
            return Ok(ApiResponse.Ok(data));
        }
        catch (ServiceException ex) { return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, [ex.ErrorCode])); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message, ["VALIDATION_ERROR"])); }
        catch (DbUpdateConcurrencyException) { return Conflict(ApiResponse.Fail("Dữ liệu đã thay đổi. Hãy tải lại bài.")); }
        catch (Exception ex) when (CommunityDatabaseErrors.IsConflict(ex)) { return Conflict(ApiResponse.Fail("Có thao tác đồng thời. Tải lại bài để kiểm tra kết quả trước khi gửi lại.")); }
        catch (Exception ex) { logger.LogError(ex, "Product group operation failed"); return StatusCode(500, ApiResponse.Fail("Chưa thể hoàn tất thao tác. Tải lại bài để kiểm tra kết quả.")); }
    }
    [HttpPost("/api/stores/{storeId:int}/product-groups")]
    public Task<IActionResult> Create(int storeId, CreateProductGroupRequest request) => Run(id => service.CreateAsync(id, storeId, request), create: true);
    [HttpPut("{groupId:int}")]
    public Task<IActionResult> Update(int groupId, UpdateProductGroupRequest request) => Run(id => service.UpdateAsync(id, groupId, request));
    [HttpGet("mine")]
    public Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 10) => Run(id => service.MineAsync(id, page, pageSize));
    [HttpGet("{groupId:int}/manage")]
    public Task<IActionResult> Manage(int groupId) => Run(id => service.ManageAsync(id, groupId));
    [HttpGet("/api/stores/{storeId:int}/product-groups"), AllowAnonymous]
    public Task<IActionResult> Store(int storeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 12) => Run(_ => service.StoreAsync(storeId, page, pageSize), anonymous: true);
    [HttpGet("{groupId:int}"), AllowAnonymous]
    public Task<IActionResult> Detail(int groupId) => Run(_ => service.DetailAsync(groupId), anonymous: true);
}
