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
                if (product.Status != "AVAILABLE" || !product.IsPublished)
                {
                    _logger.LogWarning("[PRODUCT LOCK] Cannot lock ProductId {ProductId}. Current status: {Status}", productId, product.Status);
                    return false;
                }

                product.Status = "LOCKED"; product.ReservedOrderId = orderId;
                product.UpdatedAt = DateTime.UtcNow;
                try { await _context.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException) { return false; }
                _logger.LogInformation("[PRODUCT LOCK] Updated DB ProductId {ProductId} to LOCKED for OrderId {OrderId}", productId, orderId);
                return true;
            }

            _logger.LogInformation("[PRODUCT LOCK] (Missing product) Cannot lock ProductId {ProductId} for OrderId {OrderId} (15 mins)", productId, orderId);
            return false;
        }

        public async Task UnlockProductAsync(int productId, int? orderId = null)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                if (orderId.HasValue && product.ReservedOrderId != orderId.Value) return;
                product.Status = "AVAILABLE"; product.ReservedOrderId = null;
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("[PRODUCT UNLOCK] Updated DB ProductId {ProductId} to AVAILABLE", productId);
                return;
            }

            _logger.LogInformation("[PRODUCT UNLOCK] (Mock/External) Successfully unlocked ProductId {ProductId} back to AVAILABLE", productId);
        }

        public async Task MarkProductAsSoldAsync(int productId, int? orderId = null)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                if (orderId.HasValue && product.ReservedOrderId != orderId.Value) throw new InvalidOperationException("Sản phẩm không thuộc lượt giữ của đơn hàng này.");
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
