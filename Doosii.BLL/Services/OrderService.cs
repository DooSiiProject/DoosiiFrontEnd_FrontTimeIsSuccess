using System.Data;
using Microsoft.Extensions.Configuration;
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

        private readonly IConfiguration _configuration;
        public const decimal EscrowFeeRate = 0.025m; // 2.5% platform escrow fee

        public OrderService(
            AppDbContext context,
            IProductLockService productLockService,
            ILogger<OrderService> logger, IConfiguration configuration)
        {
            _context = context;
            _productLockService = productLockService;
            _logger = logger; _configuration = configuration;
        }

        private async Task<Doosii.DAL.Models.Store.Product> PurchasableAsync(int buyerId, int productId, bool locked)
        {
            var buyer = await _context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == buyerId);
            if (buyer == null || !new[] { "CasualUser", "CasualSeller", "Shopper" }.Contains(buyer.Role)) throw new UnauthorizedAccessException("Tài khoản không được mua hàng.");
            var query = locked ? _context.Products.FromSqlInterpolated($"SELECT * FROM [store].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {productId}") : _context.Products.AsNoTracking().Where(p => p.Id == productId);
            var product = await query.Include(p => p.Images).Include(p => p.Store).ThenInclude(st => st.Owner).ThenInclude(u => u.MerchantProfile).SingleOrDefaultAsync()
                ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            if (locked)
            {
                await PickupReservationLifecycle.ExpireProductAsync(_context, product, DateTime.UtcNow);
                // Keep eligibility stable without taking serializable range locks on the orders index.
                await _context.Stores.FromSqlInterpolated($"SELECT * FROM [store].[Stores] WITH (HOLDLOCK) WHERE [Id] = {product.StoreId}").LoadAsync();
                await _context.Users.FromSqlInterpolated($"SELECT * FROM [auth].[Users] WITH (HOLDLOCK) WHERE [Id] = {product.Store.OwnerId}").LoadAsync();
                await _context.MerchantProfiles.FromSqlInterpolated($"SELECT * FROM [auth].[MerchantProfiles] WITH (HOLDLOCK) WHERE [UserId] = {product.Store.OwnerId}").LoadAsync();
                await _context.Entry(product.Store).ReloadAsync();
                await _context.Entry(product.Store.Owner).ReloadAsync();
                if (product.Store.Owner.MerchantProfile != null) await _context.Entry(product.Store.Owner.MerchantProfile).ReloadAsync();
            }
            if (product.Store.OwnerId == buyerId) throw new InvalidOperationException("Bạn không thể mua sản phẩm của mình.");
            if (!product.IsPublished || !product.Store.IsActive || product.Store.Owner.Role != "Shopper" || product.Store.Owner.MerchantProfile?.KycStatus != "APPROVED") throw new InvalidOperationException("Sản phẩm hoặc shop không còn được bán.");
            if (product.Status != "AVAILABLE" || product.ReservedOrderId != null || product.ReservedPickupReservationId != null) throw new InvalidOperationException("Sản phẩm đang được giữ hoặc đã bán.");
            return product;
        }
        private OrderQuoteResponse Quote(Doosii.DAL.Models.Store.Product product)
        {
            var shipping = _configuration.GetValue<decimal?>("Orders:ShippingFee") ?? 30000m;
            if (shipping < 0 || shipping != decimal.Truncate(shipping)) throw new InvalidOperationException("Phí vận chuyển trên máy chủ không hợp lệ.");
            var fee = Math.Round(product.Price * EscrowFeeRate, 0, MidpointRounding.AwayFromZero);
            return new OrderQuoteResponse { ProductId = product.Id, SellerId = product.Store.OwnerId, ProductName = product.Title,
                Thumbnail = product.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).FirstOrDefault()?.ImageUrl,
                ProductPrice = product.Price, ShippingFee = shipping, EscrowFee = fee, EscrowFeeRate = EscrowFeeRate,
                TotalAmount = product.Price + shipping + fee, ProductVersion = Convert.ToBase64String(product.RowVersion) };
        }
        public async Task<OrderQuoteResponse> QuoteAsync(int buyerId, int productId)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            var product = await PurchasableAsync(buyerId, productId, true);
            var quote = Quote(product); await tx.CommitAsync(); return quote;
        }
        public async Task<OrderResponse> CreateEscrowOrderAsync(int buyerId, CreateEscrowOrderRequest request)
        {
            if (request.ClientRequestId == Guid.Empty) throw new InvalidOperationException("Mã yêu cầu không hợp lệ.");
            if (request.PaymentMethod is not ("PAYOS" or "COD")) throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await _context.Users.FromSqlInterpolated($"SELECT * FROM [auth].[Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {buyerId}").LoadAsync();
            var existing = await _context.Orders.Include(o => o.Buyer).Include(o => o.Seller).Include(o => o.Items).SingleOrDefaultAsync(o => o.BuyerId == buyerId && o.ClientRequestId == request.ClientRequestId);
            if (existing != null) {
                if (existing.PaymentMethod != request.PaymentMethod || existing.Items.All(i => i.ProductId != request.ProductId)) throw new InvalidOperationException("Mã yêu cầu đã được dùng cho đơn hàng khác.");
                await tx.CommitAsync(); return MapToOrderResponse(existing, existing.Buyer.FullName, existing.Seller.FullName);
            }
            var product = await PurchasableAsync(buyerId, request.ProductId, true);
            if (Convert.ToBase64String(product.RowVersion) != request.ProductVersion) throw new InvalidOperationException("Sản phẩm đã thay đổi. Tải lại giá trước khi đặt hàng.");
            var quote = Quote(product);
            if (request.PaymentMethod == "COD") { quote.TotalAmount -= quote.EscrowFee; quote.EscrowFee = 0; }
            var order = new Order { BuyerId = buyerId, SellerId = quote.SellerId, ProductPrice = quote.ProductPrice, ShippingFee = quote.ShippingFee,
                PaymentMethod = request.PaymentMethod, Status = request.PaymentMethod == "COD" ? OrderStatus.CodPending : OrderStatus.PendingPayment,
                EscrowFee = quote.EscrowFee, TotalAmount = quote.TotalAmount, ClientRequestId = request.ClientRequestId,
                ReceiverName = request.ReceiverName.Trim(), ReceiverPhone = request.ReceiverPhone.Trim(), ShippingAddress = request.ShippingAddress.Trim(),
                Items = new List<OrderItem> { new() { ProductId = product.Id, ProductNameSnapshot = quote.ProductName, PriceSnapshot = quote.ProductPrice, ThumbnailSnapshot = quote.Thumbnail } } };
            _context.Orders.Add(order); await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context);
            if (!await _productLockService.LockProductAsync(product.Id, order.Id)) throw new InvalidOperationException("Không thể giữ sản phẩm cho đơn hàng.");
            await tx.CommitAsync();
            return await GetOrderByIdAsync(order.Id, buyerId);
        }

        public async Task<OrderResponse> GetOrderByIdAsync(int orderId, int userId)
        {
            var order = await _context.Orders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Include(o => o.Items)
                .Include(o => o.Dispute)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đơn hàng với mã #{orderId}.");
            }

            var viewer = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (viewer == null || viewer.Role == "Banned") throw new UnauthorizedAccessException("Tài khoản không có quyền xem đơn.");
            // Người xem phải là người mua, người bán hoặc admin hiện tại
            if (order.BuyerId != userId && order.SellerId != userId && viewer.Role != "Admin")
            {
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập thông tin đơn hàng này.");
            }

            var response = MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
            // Resolve from ordered products, never infer a shop from the seller alone.
            var productIds = order.Items.Select(i => i.ProductId).Distinct().ToArray();
            var products = await _context.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.StoreId, StoreName = p.Store.Name, StoreOwnerId = p.Store.OwnerId })
                .ToListAsync();
            if (productIds.Length > 0 && products.Count == productIds.Length &&
                products.All(p => p.StoreOwnerId == order.SellerId) && products.Select(p => p.StoreId).Distinct().Count() == 1)
            {
                response.StoreId = products[0].StoreId;
                response.StoreName = products[0].StoreName;
            }
            return response;
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
            await using var tx = await _context.Database.BeginTransactionAsync();
            var order = await OrderLifecycle.LockAsync(_context, orderId);
            if (order.SellerId != sellerId) throw new UnauthorizedAccessException("Chỉ người bán của đơn được cập nhật vận chuyển.");
            if (order.Buyer.Role == "Banned" || order.Seller.Role == "Banned") throw new UnauthorizedAccessException("Tài khoản liên quan đang bị khóa.");
            OrderLifecycle.EnsureVersion(order, request.Version);
            if (order.Status is not (OrderStatus.EscrowHolding or OrderStatus.CodPending)) throw new InvalidOperationException("Đơn chưa sẵn sàng để gửi hàng.");
            order.ShippingCarrier = request.ShippingCarrier.Trim(); order.TrackingCode = request.TrackingCode.Trim(); order.PackageImageUrl = request.PackageImageUrl?.Trim();
            order.Status = order.PaymentMethod == "COD" ? OrderStatus.CodInTransit : OrderStatus.InTransit; order.ShippedAt = DateTime.UtcNow; order.UpdatedAt = DateTime.UtcNow;
            await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); await tx.CommitAsync();
            return MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
        }
        public async Task<OrderResponse> ConfirmOrderReceivedAsync(int orderId, int buyerId, ConfirmOrderRequest request)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            var order = await OrderLifecycle.LockAsync(_context, orderId);
            if (order.BuyerId != buyerId) throw new UnauthorizedAccessException("Chỉ người mua của đơn được xác nhận nhận hàng.");
            OrderLifecycle.EnsureVersion(order, request.Version);
            if (order.Status != OrderStatus.InTransit || (order.Dispute != null && order.Dispute.Status != "RESOLVED")) throw new InvalidOperationException("Đơn chưa được gửi hoặc đang có khiếu nại.");
            await CompleteAsync(order); await tx.CommitAsync();
            return MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
        }
        private async Task CompleteAsync(Order order)
        {
            var escrow = order.EscrowTransactions.SingleOrDefault(e => e.Status == EscrowStatus.Holding)
                ?? throw new InvalidOperationException("Không tìm thấy khoản ký quỹ đã nhận của đơn hàng.");
            order.Status = OrderStatus.CompletedReleased; order.CompletedAt = DateTime.UtcNow; order.DeliveredAt ??= DateTime.UtcNow; order.UpdatedAt = DateTime.UtcNow;
            escrow.Status = EscrowStatus.Released; escrow.ReleasedAt = DateTime.UtcNow;
            foreach (var item in order.Items) await _productLockService.MarkProductAsSoldAsync(item.ProductId, order.Id);
            await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context);
        }

        public async Task<OrderResponse> CancelAsync(int orderId, int buyerId, ConfirmOrderRequest request)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            var order = await OrderLifecycle.LockAsync(_context, orderId);
            if (order.BuyerId != buyerId || order.Buyer.Role == "Banned") throw new UnauthorizedAccessException("Chỉ người mua được hủy đơn của mình.");
            if (order.Status == OrderStatus.Cancelled) return MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
            OrderLifecycle.EnsureVersion(order, request.Version);
            if (order.Status is not (OrderStatus.PendingPayment or OrderStatus.CodPending) || order.PaidAt.HasValue || order.EscrowTransactions.Any()) throw new InvalidOperationException("Chỉ hủy đơn chưa thanh toán và chưa gửi hàng.");
            order.Status = OrderStatus.Cancelled; order.UpdatedAt = DateTime.UtcNow;
            foreach (var item in order.Items) {
                var product = await _context.Products.FromSqlInterpolated($"SELECT * FROM [store].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {item.ProductId}").SingleAsync();
                if (product.Status == "LOCKED" && product.ReservedOrderId == order.Id && product.ReservedPickupReservationId == null) { product.Status = "AVAILABLE"; product.ReservedOrderId = null; product.UpdatedAt = DateTime.UtcNow; }
            }
            await _context.SaveChangesAsync(); await tx.CommitAsync();
            return MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
        }

        public async Task<OrderResponse> CollectCodAsync(int orderId, int sellerId, ConfirmOrderRequest request)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            var order = await OrderLifecycle.LockAsync(_context, orderId);
            if (order.SellerId != sellerId || order.Seller.Role == "Banned") throw new UnauthorizedAccessException("Chỉ shop của đơn được xác nhận thu tiền COD.");
            if (order.Status == OrderStatus.CodCompleted) return MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
            OrderLifecycle.EnsureVersion(order, request.Version);
            if (order.PaymentMethod != "COD" || order.Status != OrderStatus.CodInTransit) throw new InvalidOperationException("Đơn không phải COD đang giao.");
            order.Status = OrderStatus.CodCompleted; order.PaidAt = order.DeliveredAt = order.CompletedAt = order.UpdatedAt = DateTime.UtcNow;
            foreach (var item in order.Items) {
                await _context.Products.FromSqlInterpolated($"SELECT * FROM [store].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {item.ProductId}").LoadAsync();
                await _productLockService.MarkProductAsSoldAsync(item.ProductId, order.Id);
            }
            await _context.SaveChangesAsync(); await tx.CommitAsync();
            return MapToOrderResponse(order, order.Buyer.FullName, order.Seller.FullName);
        }

        public async Task<int> CancelExpiredOrdersAsync()
        {
            var threshold = DateTime.UtcNow.AddMinutes(-15);
            var ids = await _context.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.PendingPayment && o.CreatedAt <= threshold).Select(o => o.Id).ToListAsync();
            var count = 0;
            foreach (var id in ids)
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                var order = await OrderLifecycle.LockAsync(_context, id);
                await _context.Entry(order).ReloadAsync();
                if (await OrderLifecycle.ExpireAsync(_context, order)) count++;
                await tx.CommitAsync();
            }
            return count;
        }
        public async Task<int> AutoCompleteDeliveredOrdersAsync()
        {
            // A shipping timestamp alone is not proof that delivery occurred.
            var threshold = DateTime.UtcNow.AddHours(-72);
            var ids = await _context.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.InTransit && o.DeliveredAt.HasValue && o.DeliveredAt <= threshold).Select(o => o.Id).ToListAsync();
            var count = 0;
            foreach (var id in ids)
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                var order = await OrderLifecycle.LockAsync(_context, id); await _context.Entry(order).ReloadAsync();
                if (order.Status == OrderStatus.InTransit && order.DeliveredAt <= threshold && order.Dispute == null) { await CompleteAsync(order); count++; }
                await tx.CommitAsync();
            }
            return count;
        }

        public async Task<DisputeResponse> CreateDisputeAsync(int orderId, int buyerId, CreateDisputeRequest request)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var order = await OrderLifecycle.LockAsync(_context, orderId);
            if (order.Buyer.Role == "Banned") throw new UnauthorizedAccessException("Tài khoản đang bị khóa.");
            if (order == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đơn hàng #{orderId}.");
            }

            if (order.BuyerId != buyerId)
            {
                throw new UnauthorizedAccessException("Bạn không phải người mua của đơn hàng này.");
            }

            if (order.Dispute != null)
            {
                throw new InvalidOperationException("Đơn hàng này đã có khiếu nại đang được xử lý.");
            }

            if (!DisputeRules.Eligible(order, DateTime.UtcNow))
                throw new InvalidOperationException("Chỉ được khiếu nại trong quá trình giao hàng hoặc tối đa 24 giờ sau khi nhận hàng.");
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
                throw new InvalidOperationException("Lý do cần từ 1 đến 1000 ký tự.");
            var video = DisputeRules.EvidenceUrl(request.EvidenceVideoUrl);
            if (request.EvidenceImages?.Count > 5) throw new InvalidOperationException("Tối đa 5 ảnh bằng chứng.");
            var images = (request.EvidenceImages ?? new()).Select(DisputeRules.EvidenceUrl).Where(u => u != null).Select(u => u!).Distinct().ToList();
            if (video == null && images.Count < 2)
                throw new InvalidOperationException("Cần video mở hàng hoặc ít nhất 2 ảnh bằng chứng khác nhau.");
            if (order.EscrowTransactions.Count(e => e.Status == EscrowStatus.Holding || e.Status == EscrowStatus.Released) != 1)
                throw new InvalidOperationException("Không xác định được khoản ký quỹ của đơn hàng.");

            // Đổi trạng thái đơn hàng sang DISPUTED (đóng băng ký quỹ)
            order.Status = OrderStatus.Disputed;
            order.UpdatedAt = DateTime.UtcNow;

            var dispute = new Dispute
            {
                OrderId = order.Id,
                RaisedByUserId = buyerId,
                Reason = request.Reason.Trim(),
                EvidenceVideoUrl = video,
                EvidenceImagesJson = images.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(images)
                    : null,
                Status = DisputeStatus.PendingReview,
                CreatedAt = DateTime.UtcNow
            };

            _context.Disputes.Add(dispute);
            await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context);
            await transaction.CommitAsync();

            _logger.LogInformation("Dispute #{DisputeId} opened by Buyer #{BuyerId} for Order #{OrderId}. Status: {Status}",
                dispute.Id, buyerId, order.Id, dispute.Status);

            return MapToDisputeResponse(dispute, order.Buyer.FullName);
        }

        public async Task<DisputeResponse?> GetOrderDisputeAsync(int orderId, int userId)
        {
            var actor = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (actor == null || actor.Role == "Banned") throw new UnauthorizedAccessException("Tài khoản không có quyền xem khiếu nại.");
            var order = await _context.Orders
                .Include(o => o.Buyer)
                .Include(o => o.Dispute)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy đơn hàng #{orderId}.");
            }

            if (order.BuyerId != userId && order.SellerId != userId)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null || user.Role != "Admin")
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền xem khiếu nại của đơn hàng này.");
                }
            }

            if (order.Dispute == null)
            {
                return null;
            }

            return MapToDisputeResponse(order.Dispute, order.Buyer.FullName);
        }

        private static DisputeResponse MapToDisputeResponse(Dispute dispute, string buyerName)
        {
            var images = new List<string>();
            if (!string.IsNullOrWhiteSpace(dispute.EvidenceImagesJson))
            {
                try
                {
                    images = System.Text.Json.JsonSerializer.Deserialize<List<string>>(dispute.EvidenceImagesJson) ?? new List<string>();
                }
                catch { }
            }

            return new DisputeResponse
            {
                Id = dispute.Id,
                OrderId = dispute.OrderId,
                RaisedByUserId = dispute.RaisedByUserId,
                RaisedByUserName = buyerName,
                Reason = dispute.Reason,
                EvidenceVideoUrl = dispute.EvidenceVideoUrl,
                EvidenceImages = images,
                Status = dispute.Status,
                AdminVerdict = dispute.AdminVerdict,
                CreatedAt = dispute.CreatedAt,
                ResolvedAt = dispute.ResolvedAt
            };
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
                PaymentMethod = order.PaymentMethod,
                ReceiverName = order.ReceiverName,
                ReceiverPhone = order.ReceiverPhone,
                ShippingAddress = order.ShippingAddress,
                ShippingCarrier = order.ShippingCarrier,
                TrackingCode = order.TrackingCode,
                PackageImageUrl = order.PackageImageUrl,
                PaidAt = order.PaidAt,
                ShippedAt = order.ShippedAt,
                CompletedAt = order.CompletedAt,
                CreatedAt = DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc),
                PaymentExpiresAt = OrderLifecycle.ExpiresAt(order), Version = Convert.ToBase64String(order.RowVersion),
                DeliveredAt = order.DeliveredAt, DisputeDeadline = DisputeRules.Deadline(order),
                CanOpenDispute = DisputeRules.Eligible(order, DateTime.UtcNow),
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
