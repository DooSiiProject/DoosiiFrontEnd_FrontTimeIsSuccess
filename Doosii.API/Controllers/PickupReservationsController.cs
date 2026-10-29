using System.Security.Claims;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Doosii.API.Controllers;

[ApiController, Authorize, Route("api/pickup-reservations"), PickupValidation]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PickupReservationsController(IPickupReservationService reservations, ILogger<PickupReservationsController> logger) : ControllerBase
{
    private int Actor => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new ServiceException(401, "Vui lòng đăng nhập.", "UNAUTHORIZED");
    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (PickupCodeAttemptException error)
        {
            if (error.RetryAfterSeconds > 0) Response.Headers.RetryAfter = error.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var response = ApiResponse.Fail(error.Message, new() { error.ErrorCode });
            response.Data = new { version = error.Version, retryAfterSeconds = error.RetryAfterSeconds };
            return StatusCode(error.StatusCode, response);
        }
        catch (ServiceException error) { return StatusCode(error.StatusCode, ApiResponse.Fail(error.Message, new() { error.ErrorCode })); }
        catch (DbUpdateConcurrencyException) { return Conflict(ApiResponse.Fail("Dữ liệu đã thay đổi. Hãy tải lại.", new() { "PICKUP_CONFLICT" })); }
        catch (Exception error) when (ConflictError(error)) { return Conflict(ApiResponse.Fail("Có thao tác đồng thời. Hãy tải lại và thử lại.", new() { "PICKUP_CONFLICT" })); }
        catch (Exception error) { logger.LogError(error, "Pickup reservation request failed"); return StatusCode(500, ApiResponse.Fail("Không xử lý được lượt giữ.", new() { "INTERNAL_ERROR" })); }
    }
    private static bool ConflictError(Exception error)
    {
        for (Exception? current = error; current != null; current = current.InnerException)
            if (current is SqlException { Number: 1205 or 2601 or 2627 or 51010 }) return true;
        return false;
    }
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreatePickupReservationRequest request) => Execute(async () => {
        var result = await reservations.CreateAsync(Actor, request);
        return result.Replayed ? Ok(ApiResponse.Ok(result.Reservation)) : CreatedAtAction(nameof(Detail), new { id = result.Reservation.Id }, ApiResponse.Ok(result.Reservation));
    });
    [HttpGet("mine")]
    public Task<IActionResult> Mine([FromQuery] PickupReservationQuery query) => Execute(async () => Ok(ApiResponse.Ok(await reservations.ListAsync(Actor, query, false))));
    [HttpGet("shop")]
    public Task<IActionResult> Shop([FromQuery] PickupReservationQuery query) => Execute(async () => Ok(ApiResponse.Ok(await reservations.ListAsync(Actor, query, true))));
    [HttpGet("{id:int}")]
    public Task<IActionResult> Detail(int id) => Execute(async () => Ok(ApiResponse.Ok(await reservations.DetailAsync(Actor, id))));
    [HttpPost("{id:int}/accept")]
    public Task<IActionResult> Accept(int id, [FromBody] PickupVersionRequest request) => Execute(async () => Ok(ApiResponse.Ok(await reservations.AcceptAsync(Actor, id, request))));
    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, [FromBody] ClosePickupReservationRequest request) => Execute(async () => Ok(ApiResponse.Ok(await reservations.RejectAsync(Actor, id, request))));
    [HttpPost("{id:int}/cancel")]
    public Task<IActionResult> Cancel(int id, [FromBody] ClosePickupReservationRequest request) => Execute(async () => Ok(ApiResponse.Ok(await reservations.CancelAsync(Actor, id, request))));
    [HttpGet("{id:int}/pickup-code")]
    public Task<IActionResult> Code(int id) => Execute(async () => Ok(ApiResponse.Ok(await reservations.CodeAsync(Actor, id))));
    [HttpPost("{id:int}/complete")]
    public Task<IActionResult> Complete(int id, [FromBody] CompletePickupReservationRequest request) => Execute(async () => Ok(ApiResponse.Ok(await reservations.CompleteAsync(Actor, id, request))));
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class PickupValidationAttribute : ActionFilterAttribute
{
    public PickupValidationAttribute() { Order = -3000; }
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
            context.Result = new BadRequestObjectResult(ApiResponse.Fail("Dữ liệu giữ đồ không hợp lệ.", new() { "INVALID_REQUEST" }));
    }
}
