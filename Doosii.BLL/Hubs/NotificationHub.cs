using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Doosii.DAL.Data;

namespace Doosii.BLL.Hubs;

[Authorize]
public class NotificationHub(AppDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!int.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) || id <= 0 ||
            !await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.Role != "Banned"))
        {
            Context.Abort();
            throw new HubException("UNAUTHORIZED");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{id}");
        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast_channel");
        await base.OnConnectedAsync();
    }
    // SignalR automatically removes the connection from all groups on disconnect.
}
