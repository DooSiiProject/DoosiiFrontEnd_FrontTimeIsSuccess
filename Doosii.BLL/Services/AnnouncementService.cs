using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.BLL.Exceptions;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Store;
namespace Doosii.BLL.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly AppDbContext _context;
    private readonly IWalletService _walletService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AnnouncementService> _logger;
    public const decimal BroadcastFee = 50000m;
    public const int DailyLimit = 2;
    public AnnouncementService(AppDbContext context, IWalletService walletService, INotificationService notificationService, ILogger<AnnouncementService> logger)
    { _context = context; _walletService = walletService; _notificationService = notificationService; _logger = logger; }
    public async Task<AnnouncementOptions> GetOptionsAsync(int sellerId)
    {
        await WalletRules.EnsureSellerAsync(_context, sellerId);
        var today = DateTime.UtcNow.Date;
        var count = await _context.BaleAnnouncements.CountAsync(a => a.SellerId == sellerId && a.CreatedAt >= today && a.CreatedAt < today.AddDays(1));
        return new AnnouncementOptions { BroadcastFee = BroadcastFee, DailyLimit = DailyLimit, RemainingToday = Math.Max(0, DailyLimit - count), ResetsAt = DateTime.SpecifyKind(today.AddDays(1), DateTimeKind.Utc) };
    }
    public async Task<AnnouncementResponse> CreateAnnouncementAsync(int sellerId, CreateAnnouncementRequest request)
    {
        var title = request.Title?.Trim() ?? ""; var description = request.Description?.Trim(); var banner = request.BannerUrl?.Trim();
        if (title.Length is < 1 or > 200 || description?.Length > 1000 || request.StoreId <= 0 || !request.ClientRequestId.HasValue || request.ClientRequestId == Guid.Empty || request.ExpectedFee != BroadcastFee)
            throw new ArgumentException("Kiểm tra tiêu đề, mô tả, cửa hàng, mã yêu cầu và phí. Hãy tải lại biểu mẫu.");
        if (string.IsNullOrWhiteSpace(banner)) banner = null;
        else if (banner.Length > 500 || !Uri.TryCreate(banner, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)) throw new ArgumentException("Banner cần là URL HTTPS hợp lệ, tối đa 500 ký tự.");
        if (request.PaymentMethod != "WALLET") throw new ServiceException(400, "Hiện chỉ hỗ trợ thanh toán bằng ví DooSii.", "PAYMENT_UNAVAILABLE");
        var eventTime = request.EventTime.ToUniversalTime();
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await WalletRules.LockAsync(_context, sellerId); await WalletRules.EnsureSellerAsync(_context, sellerId);
        var store = await _context.Stores.Include(s => s.Owner).ThenInclude(u => u.MerchantProfile).SingleOrDefaultAsync(s => s.Id == request.StoreId) ?? throw new KeyNotFoundException("Không tìm thấy shop.");
        if (store.OwnerId != sellerId) throw new ServiceException(403, "Bạn không phải chủ shop.", "NOT_OWNER");
        var existing = await _context.BaleAnnouncements.Include(a => a.Store).Include(a => a.Seller).SingleOrDefaultAsync(a => a.SellerId == sellerId && a.ClientRequestId == request.ClientRequestId);
        if (existing != null)
        {
            if (existing.StoreId != request.StoreId || existing.Title != title || existing.Description != description || existing.BannerUrl != banner || existing.EventTime != eventTime || existing.BroadcastFee != request.ExpectedFee || existing.PaymentMethod != request.PaymentMethod)
                throw new ServiceException(409, "Mã yêu cầu đã dùng với nội dung khác.", "IDEMPOTENCY_CONFLICT");
            await transaction.CommitAsync(); return Map(existing);
        }
        if (!store.IsActive || store.Owner.MerchantProfile?.KycStatus != "APPROVED") throw new ServiceException(403, "Shop cần đang hoạt động và được duyệt KYC để đăng sự kiện.", "INACTIVE_SHOP");
        if (eventTime <= DateTime.UtcNow) throw new ArgumentException("Thời gian sự kiện phải ở tương lai.");
        var today = DateTime.UtcNow.Date;
        if (await _context.BaleAnnouncements.CountAsync(a => a.StoreId == store.Id && a.CreatedAt >= today && a.CreatedAt < today.AddDays(1)) >= DailyLimit)
            throw new ServiceException(409, "Shop đã dùng hết 2 lượt đăng trong ngày. Hãy tải lại để xem thời gian mở lượt mới.", "DAILY_LIMIT");
        var wallet = await _walletService.GetSellerWalletAsync(sellerId);
        if (wallet.AvailableBalance < BroadcastFee) throw new ServiceException(409, "Số dư khả dụng chưa đủ phí đăng 50.000 VND. Hãy kiểm tra ví.", "INSUFFICIENT_FUNDS");
        var announcement = new BaleAnnouncement { Store = store, SellerId = sellerId, Title = title, Description = description, BannerUrl = banner, EventTime = eventTime, BroadcastFee = BroadcastFee, PaymentMethod = "WALLET", Status = AnnouncementStatus.PaidActive, ClientRequestId = request.ClientRequestId, CreatedAt = DateTime.UtcNow };
        _context.BaleAnnouncements.Add(announcement); await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); await transaction.CommitAsync();
        // A notification transport failure must never turn a committed fee into a failed purchase.
        try { await _notificationService.BroadcastNotificationAsync($"Sự kiện khui kiện từ {store.Name}", $"{title} · {eventTime.AddHours(7):HH:mm dd/MM/yyyy} (giờ Việt Nam)", "BALE_OPENING", $"/shop/{store.Id}"); }
        catch (Exception ex) { _logger.LogWarning(ex, "Announcement {Id} saved; notification delivery failed", announcement.Id); }
        return Map(announcement);
    }
    public async Task<List<AnnouncementResponse>> GetSellerAnnouncementsAsync(int sellerId)
    {
        await WalletRules.EnsureSellerAsync(_context, sellerId);
        return (await _context.BaleAnnouncements.AsNoTracking().Include(a => a.Store).Include(a => a.Seller).Where(a => a.SellerId == sellerId).OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).ToListAsync()).Select(Map).ToList();
    }
    public async Task<List<AnnouncementResponse>> GetUpcomingAnnouncementsAsync(int? storeId = null)
    {
        if (storeId <= 0) throw new ArgumentException("Mã shop không hợp lệ.");
        var now = DateTime.UtcNow;
        var query = _context.BaleAnnouncements.AsNoTracking().Include(a => a.Store).Include(a => a.Seller).Where(a => a.Status == AnnouncementStatus.PaidActive && a.PaymentMethod == "WALLET" && a.EventTime > now && a.Store.IsActive && a.Store.OwnerId == a.SellerId && a.Seller.Role == "Shopper" && a.Seller.MerchantProfile != null && a.Seller.MerchantProfile.KycStatus == "APPROVED");
        if (storeId.HasValue) query = query.Where(a => a.StoreId == storeId.Value);
        return (await query.OrderBy(a => a.EventTime).ThenBy(a => a.Id).Take(20).ToListAsync()).Select(Map).ToList();
    }
    private static AnnouncementResponse Map(BaleAnnouncement a) => new() { Id = a.Id, StoreId = a.StoreId, StoreName = a.Store.Name, StoreAddress = a.Store.Address ?? "", SellerId = a.SellerId, SellerName = a.Seller?.FullName ?? "", Title = a.Title, Description = a.Description, BannerUrl = a.BannerUrl, EventTime = DateTime.SpecifyKind(a.EventTime, DateTimeKind.Utc), BroadcastFee = a.BroadcastFee, PaymentMethod = a.PaymentMethod, Status = a.Status, CreatedAt = DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc), ClientRequestId = a.ClientRequestId };
}
