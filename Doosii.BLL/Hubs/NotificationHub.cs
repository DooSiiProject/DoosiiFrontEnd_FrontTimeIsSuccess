using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Doosii.BLL.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                // Thêm kết nối vào nhóm cá nhân: user_{userId}
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }

            // Tham gia nhóm chung nhận thông báo phát sóng / khui kiện
            await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast_channel");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "broadcast_channel");

            await base.OnDisconnectedAsync(exception);
        }
    }
}
