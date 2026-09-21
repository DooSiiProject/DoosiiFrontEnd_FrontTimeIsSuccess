using Microsoft.Extensions.Logging;
using Doosii.BLL.Interfaces;

namespace Doosii.BLL.Services
{
    public class MockProductLockService : IProductLockService
    {
        private readonly ILogger<MockProductLockService> _logger;

        public MockProductLockService(ILogger<MockProductLockService> logger)
        {
            _logger = logger;
        }

        public Task<bool> LockProductAsync(int productId, int orderId)
        {
            _logger.LogInformation("[PRODUCT LOCK] Successfully locked ProductId {ProductId} for OrderId {OrderId} (15 mins)", productId, orderId);
            return Task.FromResult(true);
        }

        public Task UnlockProductAsync(int productId)
        {
            _logger.LogInformation("[PRODUCT UNLOCK] Successfully unlocked ProductId {ProductId} back to AVAILABLE", productId);
            return Task.CompletedTask;
        }

        public Task MarkProductAsSoldAsync(int productId)
        {
            _logger.LogInformation("[PRODUCT SOLD] Successfully transitioned ProductId {ProductId} to SOLD permanently", productId);
            return Task.CompletedTask;
        }
    }
}
