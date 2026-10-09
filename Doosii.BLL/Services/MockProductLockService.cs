using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;

namespace Doosii.BLL.Services
{
    public class MockProductLockService : IProductLockService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MockProductLockService> _logger;

        public MockProductLockService(
            AppDbContext context,
            ILogger<MockProductLockService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> LockProductAsync(int productId, int orderId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                if (product.Status != "AVAILABLE")
                {
                    _logger.LogWarning("[PRODUCT LOCK] Cannot lock ProductId {ProductId}. Current status: {Status}", productId, product.Status);
                    return false;
                }

                product.Status = "LOCKED";
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("[PRODUCT LOCK] Updated DB ProductId {ProductId} to LOCKED for OrderId {OrderId}", productId, orderId);
                return true;
            }

            _logger.LogInformation("[PRODUCT LOCK] (Mock/External) Successfully locked ProductId {ProductId} for OrderId {OrderId} (15 mins)", productId, orderId);
            return true;
        }

        public async Task UnlockProductAsync(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                product.Status = "AVAILABLE";
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("[PRODUCT UNLOCK] Updated DB ProductId {ProductId} to AVAILABLE", productId);
                return;
            }

            _logger.LogInformation("[PRODUCT UNLOCK] (Mock/External) Successfully unlocked ProductId {ProductId} back to AVAILABLE", productId);
        }

        public async Task MarkProductAsSoldAsync(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                product.Status = "SOLD";
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("[PRODUCT SOLD] Updated DB ProductId {ProductId} to SOLD permanently", productId);
                return;
            }

            _logger.LogInformation("[PRODUCT SOLD] (Mock/External) Successfully transitioned ProductId {ProductId} to SOLD permanently", productId);
        }
    }
}
