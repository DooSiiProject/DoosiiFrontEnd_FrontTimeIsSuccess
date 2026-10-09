using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Hubs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Notification;

namespace Doosii.BLL.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            AppDbContext context,
            IHubContext<NotificationHub> hubContext,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<NotificationDto> SendNotificationAsync(int userId, string title, string message, string type, string? targetUrl = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title.Trim(),
                Message = message.Trim(),
                Type = type,
                TargetUrl = targetUrl?.Trim(),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            var dto = MapToNotificationDto(notification);

            // Bắn SignalR Real-time tới nhóm cá nhân user_{userId}
            try
            {
                await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi SignalR push tới user_{UserId}", userId);
            }

            return dto;
        }

        public async Task BroadcastNotificationAsync(string title, string message, string type, string? targetUrl = null)
        {
            var payload = new
            {
                Title = title.Trim(),
                Message = message.Trim(),
                Type = type,
                TargetUrl = targetUrl?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            // 1. Lưu thông báo vào DB cho tất cả người dùng (hiển thị trong chuông thông báo cá nhân)
            try
            {
                var userIds = await _context.Users.Select(u => u.Id).ToListAsync();
                var notifications = userIds.Select(uid => new Notification
                {
                    UserId = uid,
                    Title = title.Trim(),
                    Message = message.Trim(),
                    Type = type,
                    TargetUrl = targetUrl?.Trim(),
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                if (notifications.Count > 0)
                {
                    _context.Notifications.AddRange(notifications);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi lưu broadcast notification vào DB");
            }

            // 2. Bắn SignalR Broadcast real-time tới broadcast_channel
            try
            {
                await _hubContext.Clients.Group("broadcast_channel").SendAsync("ReceiveBroadcast", payload);
                _logger.LogInformation("SignalR Broadcast sent successfully: {Title}", title);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi broadcast SignalR notification");
            }
        }

        public async Task<List<NotificationDto>> GetUserNotificationsAsync(int userId, bool? unreadOnly = null)
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId)
                .AsQueryable();

            if (unreadOnly.HasValue && unreadOnly.Value)
            {
                query = query.Where(n => !n.IsRead);
            }

            var list = await query.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();

            return list.Select(MapToNotificationDto).ToList();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var unreadList = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unreadList.Count > 0)
            {
                foreach (var n in unreadList)
                {
                    n.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }
        }

        private static NotificationDto MapToNotificationDto(Notification n)
        {
            return new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                TargetUrl = n.TargetUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            };
        }
    }
}
