using Doosii.BLL.Services;

namespace Doosii.API.BackgroundServices;

public sealed class PickupMaintenanceWorker(IServiceScopeFactory scopes, ILogger<PickupMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await using var maintenance = scopes.CreateAsyncScope();
                await maintenance.ServiceProvider.GetRequiredService<PickupMaintenanceService>().RunAsync(stoppingToken);
                await using var delivery = scopes.CreateAsyncScope();
                await delivery.ServiceProvider.GetRequiredService<PickupNotificationDelivery>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning(error, "Pickup background work will retry"); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) return; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        } while (!stoppingToken.IsCancellationRequested);
    }
}
