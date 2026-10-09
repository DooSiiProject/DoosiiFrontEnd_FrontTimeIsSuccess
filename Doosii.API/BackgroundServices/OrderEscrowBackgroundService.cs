using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Doosii.BLL.Interfaces;

namespace Doosii.API.BackgroundServices
{
    public class OrderEscrowBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OrderEscrowBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Kiểm tra mỗi phút

        public OrderEscrowBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<OrderEscrowBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OrderEscrowBackgroundService started. Monitoring expired orders (15m) and auto-completion (72h)...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

                        // 1. Worker 1: Tự động hủy đơn hàng quá 15 phút chưa thanh toán và nhả lock sản phẩm
                        var cancelledCount = await orderService.CancelExpiredOrdersAsync();
                        if (cancelledCount > 0)
                        {
                            _logger.LogInformation("[BACKGROUND WORKER] Auto-cancelled {Count} unpaid orders and unlocked products.", cancelledCount);
                        }

                        // 2. Worker 2: Tự động hoàn tất đơn hàng và giải ngân sau 72h vận chuyển nếu không có tranh chấp
                        var completedCount = await orderService.AutoCompleteDeliveredOrdersAsync();
                        if (completedCount > 0)
                        {
                            _logger.LogInformation("[BACKGROUND WORKER] Auto-completed and released escrow for {Count} delivered orders past 72h.", completedCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BACKGROUND WORKER] Unexpected error occurred during order monitoring cycle.");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("OrderEscrowBackgroundService is stopping.");
        }
    }
}
