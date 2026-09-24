using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationDto> SendNotificationAsync(int userId, string title, string message, string type, string? targetUrl = null);
        Task BroadcastNotificationAsync(string title, string message, string type, string? targetUrl = null);
        Task<List<NotificationDto>> GetUserNotificationsAsync(int userId, bool? unreadOnly = null);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task MarkAllAsReadAsync(int userId);
    }
}
