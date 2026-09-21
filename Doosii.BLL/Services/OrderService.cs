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
