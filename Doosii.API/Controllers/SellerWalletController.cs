using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
namespace Doosii.API.Controllers;

[ApiController, CommunityValidation, Route("api/seller/wallet"), Authorize]
public class SellerWalletController : ControllerBase
{
    private readonly IWalletService _wallet;
    public SellerWalletController(IWalletService wallet) { _wallet = wallet; }
    private async Task<IActionResult> Execute<T>(Func<int, Task<T>> action) where T : notnull
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized(ApiResponse.Fail("Phiên không hợp lệ."));
        try { return Ok(ApiResponse.Ok(await action(id))); }
        catch (ServiceException ex) { return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (Exception ex) when (Doosii.BLL.Helpers.CommunityDatabaseErrors.IsConflict(ex)) { return Conflict(ApiResponse.Fail("Dữ liệu vừa thay đổi. Hãy tải lại ví.")); }
        catch (Exception) { return StatusCode(500, ApiResponse.Fail("Chưa thể tải hoặc cập nhật ví. Hãy tải lại để kiểm tra kết quả.")); }
    }
    [HttpGet] public Task<IActionResult> GetWallet() => Execute(_wallet.GetSellerWalletAsync);
    [HttpGet("withdrawals")] public Task<IActionResult> GetWithdrawals() => Execute(_wallet.GetMyWithdrawalRequestsAsync);
    [HttpPost("withdraw")] public Task<IActionResult> Withdraw([FromBody] CreateWithdrawalRequest request) => Execute(id => _wallet.CreateWithdrawalRequestAsync(id, request));
}
