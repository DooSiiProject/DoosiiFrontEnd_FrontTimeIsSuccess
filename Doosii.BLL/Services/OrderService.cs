using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;

namespace Doosii.BLL.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly IProductLockService _productLockService;
        private readonly ILogger<OrderService> _logger;

        public const decimal EscrowFeeRate = 0.025m; // 2.5% platform escrow fee

        public OrderService(
            AppDbContext context,
            IProductLockService productLockService,
            ILogger<OrderService> logger)
        {
            _context = context;
            _productLockService = productLockService;
            _logger = logger;
        }

        public async Task<OrderResponse> CreateEscrowOrderAsync(int buyerId, CreateEscrowOrderRequest request)
        {
            // 1. Kiểm tra tài khoản người mua và người bán
            var buyer = await _context.Users.FindAsync(buyerId);
            if (buyer == null)
            {
                throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản người mua.");
            }

            var seller = await _context.Users.FindAsync(request.SellerId);
            if (seller == null)
            {
                throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản người bán.");
            }

            if (buyerId == request.SellerId)
            {
                throw new InvalidOperationException("Bạn không thể tự đặt mua sản phẩm của chính mình.");
            }

            // 2. Tính toán phí Escrow (2.5%) và tổng tiền đơn hàng
            var escrowFee = Math.Round(request.ProductPrice * EscrowFeeRate, 0, MidpointRounding.AwayFromZero);
            var totalAmount = request.ProductPrice + request.ShippingFee + escrowFee;

            // 3. Tạo đơn hàng và snapshot thông tin sản phẩm
            var order = new Order
            {
                BuyerId = buyerId,
                SellerId = request.SellerId,
                ProductPrice = request.ProductPrice,
                ShippingFee = request.ShippingFee,
                EscrowFee = escrowFee,
                TotalAmount = totalAmount,
                Status = OrderStatus.PendingPayment,
                ReceiverName = request.ReceiverName.Trim(),
                ReceiverPhone = request.ReceiverPhone.Trim(),
                ShippingAddress = request.ShippingAddress.Trim(),
                CreatedAt = DateTime.UtcNow,
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        ProductId = request.ProductId,
                        ProductNameSnapshot = request.ProductName.Trim(),
                        PriceSnapshot = request.ProductPrice,
                        ThumbnailSnapshot = request.ProductThumbnail
                    }
                }
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // 4. Khóa sản phẩm (thời gian giữ chỗ 15 phút) qua IProductLockService
            var lockSuccess = await _productLockService.LockProductAsync(request.ProductId, order.Id);
            if (!lockSuccess)
            {
                order.Status = OrderStatus.Cancelled;
                await _context.SaveChangesAsync();
                throw new InvalidOperationException("Sản phẩm hiện đang được người khác giữ chỗ hoặc không khả dụng.");
            }

            _logger.LogInformation("Order #{OrderId} created successfully by Buyer #{BuyerId} for Product #{ProductId}. Status: {Status}",
                order.Id, buyerId, request.ProductId, order.Status);

            return MapToOrderResponse(order, buyer.FullName, seller.FullName);
        }

        public async Task<OrderResponse> GetOrderByIdAsync(int orderId, int userId)
        {
            var order = await _context.Orders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đơn hàng với mã #{orderId}.");
            }

            // Người xem phải là người mua hoặc người bán đơn hàng này
            if (order.BuyerId != userId && order.SellerId != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập thông tin đơn hàng này.");
            }

            return MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
        }

        public async Task<List<OrderResponse>> GetMyPurchasesAsync(int buyerId)
        {
            var orders = await _context.Orders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Include(o => o.Items)
                .Where(o => o.BuyerId == buyerId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return orders.Select(o => MapToOrderResponse(o, o.Buyer.FullName, o.Seller.FullName)).ToList();
        }

        public async Task<List<OrderResponse>> GetMySalesAsync(int sellerId)
        {
            var orders = await _context.Orders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Include(o => o.Items)
                .Where(o => o.SellerId == sellerId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return orders.Select(o => MapToOrderResponse(o, o.Buyer.FullName, o.Seller.FullName)).ToList();
        }

        public async Task<OrderResponse> ShipOrderAsync(int orderId, int sellerId, ShipOrderRequest request)
        {
            var order = await _context.Orders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đơn hàng với mã #{orderId}.");
            }

            if (order.SellerId != sellerId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền cập nhật thông tin vận chuyển cho đơn hàng của người bán khác.");
            }

            if (order.Status != OrderStatus.EscrowHolding)
            {
                throw new InvalidOperationException($"Chỉ có thể cập nhật vận chuyển khi đơn hàng đang ở trạng thái giữ tiền ký quỹ (ESCROW_HOLDING). Trạng thái hiện tại: {order.Status}.");
            }

            order.ShippingCarrier = request.ShippingCarrier.Trim();
            order.TrackingCode = request.TrackingCode.Trim();
            order.PackageImageUrl = request.PackageImageUrl?.Trim();
            order.Status = OrderStatus.InTransit;
            order.ShippedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Order #{OrderId} marked as IN_TRANSIT by Seller #{SellerId}. Carrier: {Carrier}, Tracking: {Tracking}",
                order.Id, sellerId, order.ShippingCarrier, order.TrackingCode);

            return MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
        }

        public async Task<OrderResponse> ConfirmOrderReceivedAsync(int orderId, int buyerId)
        {
            var order = await _context.Orders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Include(o => o.Items)
                .Include(o => o.EscrowTransactions)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đơn hàng với mã #{orderId}.");
            }

            if (order.BuyerId != buyerId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xác nhận nhận hàng cho đơn hàng của người mua khác.");
            }

            if (order.Status != OrderStatus.InTransit)
            {
                throw new InvalidOperationException($"Chỉ có thể xác nhận đã nhận hàng khi đơn hàng đang trong trạng thái vận chuyển (IN_TRANSIT). Trạng thái hiện tại: {order.Status}.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                order.Status = OrderStatus.CompletedReleased;
                order.CompletedAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;

                // Cập nhật trạng thái giải ngân ký quỹ
                var escrow = order.EscrowTransactions.FirstOrDefault(e => e.Status == EscrowStatus.Holding);
                if (escrow != null)
                {
                    escrow.Status = EscrowStatus.Released;
                    escrow.ReleasedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.EscrowTransactions.Add(new EscrowTransaction
                    {
                        OrderId = order.Id,
                        Amount = order.TotalAmount,
                        Status = EscrowStatus.Released,
                        ReleasedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Đánh dấu các sản phẩm là SOLD vĩnh viễn
                foreach (var item in order.Items)
                {
                    await _productLockService.MarkProductAsSoldAsync(item.ProductId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Order #{OrderId} confirmed received by Buyer #{BuyerId}. Escrow funds released to Seller #{SellerId}.",
                    order.Id, buyerId, order.SellerId);

                return MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xác nhận nhận hàng cho đơn hàng #{OrderId}", orderId);
                throw;
            }
        }

        public async Task<int> CancelExpiredOrdersAsync()
        {
            var expiredThreshold = DateTime.UtcNow.AddMinutes(-15);
            var expiredOrders = await _context.Orders
                .Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.PendingPayment && o.CreatedAt <= expiredThreshold)
                .ToListAsync();

            if (expiredOrders.Count == 0)
            {
                return 0;
            }

            _logger.LogInformation("Worker detected {Count} expired PENDING_PAYMENT orders past 15 mins. Cancelling and unlocking products...", expiredOrders.Count);

            foreach (var order in expiredOrders)
            {
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                foreach (var item in order.Items)
                {
                    await _productLockService.UnlockProductAsync(item.ProductId);
                }
            }

            await _context.SaveChangesAsync();
            return expiredOrders.Count;
        }

        public async Task<int> AutoCompleteDeliveredOrdersAsync()
        {
            var deliveredThreshold = DateTime.UtcNow.AddHours(-72);
            var autoOrders = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.EscrowTransactions)
                .Include(o => o.Dispute)
                .Where(o => o.Status == OrderStatus.InTransit
                         && o.ShippedAt.HasValue
                         && o.ShippedAt.Value <= deliveredThreshold
                         && (o.Dispute == null || o.Dispute.Status == "RESOLVED"))
                .ToListAsync();

            if (autoOrders.Count == 0)
            {
                return 0;
            }

            _logger.LogInformation("Worker detected {Count} IN_TRANSIT orders past 72h inspection window without dispute. Auto-completing and releasing escrow...", autoOrders.Count);

            foreach (var order in autoOrders)
            {
                order.Status = OrderStatus.CompletedReleased;
                order.CompletedAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;

                var escrow = order.EscrowTransactions.FirstOrDefault(e => e.Status == EscrowStatus.Holding);
                if (escrow != null)
                {
                    escrow.Status = EscrowStatus.Released;
                    escrow.ReleasedAt = DateTime.UtcNow;
                }

                foreach (var item in order.Items)
                {
                    await _productLockService.MarkProductAsSoldAsync(item.ProductId);
                }
            }

            await _context.SaveChangesAsync();
            return autoOrders.Count;
        }

        private static OrderResponse MapToOrderResponse(Order order, string buyerName, string sellerName)
        {
            return new OrderResponse
            {
                Id = order.Id,
                BuyerId = order.BuyerId,
                BuyerName = buyerName,
                SellerId = order.SellerId,
                SellerName = sellerName,
                ProductPrice = order.ProductPrice,
                ShippingFee = order.ShippingFee,
                EscrowFee = order.EscrowFee,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                ReceiverName = order.ReceiverName,
                ReceiverPhone = order.ReceiverPhone,
                ShippingAddress = order.ShippingAddress,
                ShippingCarrier = order.ShippingCarrier,
                TrackingCode = order.TrackingCode,
                PackageImageUrl = order.PackageImageUrl,
                PaidAt = order.PaidAt,
                ShippedAt = order.ShippedAt,
                CompletedAt = order.CompletedAt,
                CreatedAt = order.CreatedAt,
                PaymentTransferSyntax = $"DOSI {order.Id}",
                Items = order.Items.Select(item => new OrderItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductNameSnapshot = item.ProductNameSnapshot,
                    PriceSnapshot = item.PriceSnapshot,
                    ThumbnailSnapshot = item.ThumbnailSnapshot
                }).ToList()
            };
        }
    }
}
