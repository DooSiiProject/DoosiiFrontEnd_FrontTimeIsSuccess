using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Store;

namespace Doosii.BLL.Services
{
    public class AnnouncementService : IAnnouncementService
    {
        private readonly AppDbContext _context;
        private readonly IWalletService _walletService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AnnouncementService> _logger;

        public const decimal BroadcastFee = 50000m; // Phí phát sóng cố định 50,000 VND

        public AnnouncementService(
            AppDbContext context,
            IWalletService walletService,
            INotificationService notificationService,
            ILogger<AnnouncementService> logger)
        {
            _context = context;
            _walletService = walletService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<AnnouncementResponse> CreateAnnouncementAsync(int sellerId, CreateAnnouncementRequest request)
        {
            // 1. Kiểm tra cửa hàng tồn tại và thuộc quyền sở hữu của người bán
            var store = await _context.Stores
                .Include(s => s.Owner)
                .FirstOrDefaultAsync(s => s.Id == request.StoreId);

            if (store == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy cửa hàng #{request.StoreId}.");
            }

            if (store.OwnerId != sellerId)
            {
                throw new UnauthorizedAccessException("Bạn không phải chủ sở hữu của cửa hàng này.");
            }

            // 2. Kiểm tra giới hạn: Tối đa 2 thông báo khui kiện / ngày / shop (REQ-17)
            var today = DateTime.UtcNow.Date;
            var todayAnnouncementsCount = await _context.BaleAnnouncements
                .CountAsync(b => b.StoreId == request.StoreId && b.CreatedAt >= today && b.CreatedAt < today.AddDays(1));

            if (todayAnnouncementsCount >= 2)
            {
                throw new InvalidOperationException("Mỗi cửa hàng chỉ được tạo tối đa 2 thông báo khui kiện trong cùng một ngày.");
            }

            // 3. Kiểm tra thời gian sự kiện phải ở tương lai
            if (request.EventTime <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Thời gian diễn ra buổi khui kiện phải ở tương lai.");
            }

            // 4. Xử lý thanh toán phí phát sóng 50,000 VND
            string status = AnnouncementStatus.PendingPayment;
            string? paymentUrl = null;
            string? qrCodeUrl = null;

            if (request.PaymentMethod.Equals("WALLET", StringComparison.OrdinalIgnoreCase))
            {
                var wallet = await _walletService.GetSellerWalletAsync(sellerId);
                if (wallet.AvailableBalance < BroadcastFee)
                {
                    throw new InvalidOperationException($"Số dư khả dụng trong ví ({wallet.AvailableBalance:N0} VND) không đủ để thanh toán phí phát sóng 50,000 VND.");
                }

                status = AnnouncementStatus.PaidActive;
            }
            else // PAYOS
            {
                status = AnnouncementStatus.PaidActive;
                paymentUrl = $"https://pay.payos.vn/web/announcement-{DateTime.UtcNow.Ticks}";
                qrCodeUrl = $"https://img.vietqr.io/image/970422-123456789-compact2.png?amount=50000&addInfo=DOSI%20KHUIKIEN%20{request.StoreId}";
            }

            // 5. Lưu thông báo khui kiện vào DB
            var announcement = new BaleAnnouncement
            {
                StoreId = request.StoreId,
                SellerId = sellerId,
                Title = request.Title.Trim(),
                BannerUrl = request.BannerUrl?.Trim(),
                EventTime = request.EventTime,
                Description = request.Description?.Trim(),
                BroadcastFee = BroadcastFee,
                PaymentMethod = request.PaymentMethod.ToUpper(),
                Status = status,
                CreatedAt = DateTime.UtcNow
            };

            _context.BaleAnnouncements.Add(announcement);
            await _context.SaveChangesAsync();

            // 6. Phát sóng thông báo đẩy qua SignalR Hub tới người dùng
            if (status == AnnouncementStatus.PaidActive)
            {
                var broadcastTitle = $"🔔 Thông báo khui kiện từ {store.Name}!";
                var broadcastMsg = $"Sự kiện: \"{announcement.Title}\" sẽ diễn ra vào lúc {announcement.EventTime:HH:mm dd/MM/yyyy}. Địa chỉ: {store.Address}. Đừng bỏ lỡ!";
                await _notificationService.BroadcastNotificationAsync(broadcastTitle, broadcastMsg, "BALE_OPENING", $"/stores/{store.Id}");
            }

            _logger.LogInformation("Bale announcement #{Id} created for Store #{StoreId}. Status: {Status}",
                announcement.Id, request.StoreId, announcement.Status);

            return new AnnouncementResponse
            {
                Id = announcement.Id,
                StoreId = announcement.StoreId,
                StoreName = store.Name,
                StoreAddress = store.Address ?? string.Empty,
                SellerId = announcement.SellerId,
                SellerName = store.Owner?.FullName ?? string.Empty,
                Title = announcement.Title,
                BannerUrl = announcement.BannerUrl,
                EventTime = announcement.EventTime,
                Description = announcement.Description,
                BroadcastFee = announcement.BroadcastFee,
                PaymentMethod = announcement.PaymentMethod,
                Status = announcement.Status,
                CreatedAt = announcement.CreatedAt,
                PaymentUrl = paymentUrl,
                QrCodeUrl = qrCodeUrl
            };
        }

        public async Task<List<AnnouncementResponse>> GetSellerAnnouncementsAsync(int sellerId)
        {
            var list = await _context.BaleAnnouncements
                .Include(b => b.Store)
                .Include(b => b.Seller)
                .Where(b => b.SellerId == sellerId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return list.Select(b => new AnnouncementResponse
            {
                Id = b.Id,
                StoreId = b.StoreId,
                StoreName = b.Store?.Name ?? string.Empty,
                StoreAddress = b.Store?.Address ?? string.Empty,
                SellerId = b.SellerId,
                SellerName = b.Seller?.FullName ?? string.Empty,
                Title = b.Title,
                BannerUrl = b.BannerUrl,
                EventTime = b.EventTime,
                Description = b.Description,
                BroadcastFee = b.BroadcastFee,
                PaymentMethod = b.PaymentMethod,
                Status = b.Status,
                CreatedAt = b.CreatedAt
            }).ToList();
        }

        public async Task<List<AnnouncementResponse>> GetUpcomingAnnouncementsAsync()
        {
            var now = DateTime.UtcNow;
            var list = await _context.BaleAnnouncements
                .Include(b => b.Store)
                .Include(b => b.Seller)
                .Where(b => b.Status == AnnouncementStatus.PaidActive && b.EventTime >= now)
                .OrderBy(b => b.EventTime)
                .Take(20)
                .ToListAsync();

            return list.Select(b => new AnnouncementResponse
            {
                Id = b.Id,
                StoreId = b.StoreId,
                StoreName = b.Store?.Name ?? string.Empty,
                StoreAddress = b.Store?.Address ?? string.Empty,
                SellerId = b.SellerId,
                SellerName = b.Seller?.FullName ?? string.Empty,
                Title = b.Title,
                BannerUrl = b.BannerUrl,
                EventTime = b.EventTime,
                Description = b.Description,
                BroadcastFee = b.BroadcastFee,
                PaymentMethod = b.PaymentMethod,
                Status = b.Status,
                CreatedAt = b.CreatedAt
            }).ToList();
        }
    }
}
