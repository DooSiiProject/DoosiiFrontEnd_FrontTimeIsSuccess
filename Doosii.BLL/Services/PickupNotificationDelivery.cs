using Doosii.BLL.DTOs;
using Doosii.BLL.Hubs;
using Doosii.DAL.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Doosii.BLL.Services;

public sealed class PickupNotificationDelivery(AppDbContext db, IHubContext<NotificationHub> hub, TimeProvider clock, ILogger<PickupNotificationDelivery> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.PickupNotificationOutbox.AsNoTracking().Where(o => o.AvailableAt <= now).OrderBy(o => o.Id).Select(o => o.Id).Take(100).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                var item = await db.PickupNotificationOutbox.FromSqlInterpolated(
                    $"SELECT * FROM [notification].[PickupNotificationOutbox] WITH (UPDLOCK,READPAST,READCOMMITTEDLOCK) WHERE [Id]={id}")
                    .SingleOrDefaultAsync(cancellationToken);
                if (item == null || item.AvailableAt > clock.GetUtcNow().UtcDateTime) { await tx.CommitAsync(cancellationToken); continue; }
                var notice = await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == item.NotificationId, cancellationToken);
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    deadline.CancelAfter(TimeSpan.FromSeconds(5));
                    await hub.Clients.Group($"user_{notice.UserId}").SendAsync("ReceiveNotification", new NotificationDto {
                        Id = notice.Id, UserId = notice.UserId, Title = notice.Title, Message = notice.Message, Type = notice.Type,
                        TargetUrl = notice.TargetUrl, IsRead = notice.IsRead, CreatedAt = DateTime.SpecifyKind(notice.CreatedAt, DateTimeKind.Utc)
                    }, deadline.Token);
                    db.PickupNotificationOutbox.Remove(item); sent++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // No payload or transport exception text in logs; REST notification is already durable.
                    item.Attempts = Math.Min(1000, item.Attempts + 1);
                    item.AvailableAt = clock.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(6, item.Attempts))));
                    logger.LogWarning("Pickup realtime notification {NotificationId} will retry", notice.Id);
                }
                await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) { logger.LogWarning(error, "Pickup outbox item {OutboxId} will retry", id); }
            finally { db.ChangeTracker.Clear(); }
        }
        return sent;
    }
}
