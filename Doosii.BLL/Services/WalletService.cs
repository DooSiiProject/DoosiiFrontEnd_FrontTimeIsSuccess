using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.BLL.Exceptions;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;
namespace Doosii.BLL.Services;

public class WalletService : IWalletService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WalletService> _logger;
    public WalletService(AppDbContext context, ILogger<WalletService> logger) { _context = context; _logger = logger; }

    public async Task<WalletResponse> GetSellerWalletAsync(int userId)
    {
        await WalletRules.EnsureSellerAsync(_context, userId);
        return await CalculateAsync(_context, userId);
    }
    internal static async Task<WalletResponse> CalculateAsync(AppDbContext db, int userId)
    {
        var now = DateTime.UtcNow;
        var orders = await db.Orders.AsNoTracking().Include(o => o.Dispute).Include(o => o.EscrowTransactions).Where(o => o.SellerId == userId).ToListAsync();
        var withdrawals = await db.WithdrawalRequests.AsNoTracking().Where(w => w.UserId == userId).ToListAsync();
        var announcements = await db.BaleAnnouncements.AsNoTracking().Where(b => b.SellerId == userId && b.PaymentMethod == "WALLET" && b.Status == AnnouncementStatus.PaidActive).ToListAsync();
        decimal revenue = 0, mature = 0, holding = 0, awaiting = 0;
        var transactions = new List<WalletTransactionDto>();
        foreach (var order in orders)
        {
            // Existing policy: platform fee is deducted from seller payout.
            var payout = Math.Max(0, order.ProductPrice + order.ShippingFee - order.EscrowFee);
            if (order.Status == OrderStatus.CompletedReleased)
            {
                revenue += payout;
                var deadline = DisputeRules.Deadline(order);
                var released = order.EscrowTransactions.Count == 1 && order.EscrowTransactions.Single().Status == EscrowStatus.Released && order.TotalAmount > 0 && order.EscrowTransactions.Single().Amount == order.TotalAmount;
                if (released && deadline.HasValue && deadline.Value < now && order.Dispute?.Status != "PENDING_REVIEW") mature += payout;
                else awaiting += payout;
                transactions.Add(new WalletTransactionDto { Id = $"ORD-{order.Id}", Type = "ORDER_PAYOUT", Amount = payout, Description = $"Tiền bán hàng đơn #{order.Id} (Đã trừ phí sàn)", Status = "COMPLETED", ReferenceId = order.Id.ToString(), CreatedAt = order.CompletedAt ?? order.UpdatedAt ?? order.CreatedAt });
            }
            else if (order.Status is OrderStatus.EscrowHolding or OrderStatus.InTransit or OrderStatus.Disputed) holding += payout;
        }
        var pending = withdrawals.Where(w => w.Status == WithdrawalStatus.Pending).Sum(w => w.Amount);
        var approved = withdrawals.Where(w => w.Status == WithdrawalStatus.Approved).Sum(w => w.Amount);
        foreach (var w in withdrawals) transactions.Add(new WalletTransactionDto { Id = $"WDR-{w.Id}", Type = "WITHDRAWAL", Amount = -w.Amount, Description = $"Yêu cầu rút về {w.BankName} · ****{w.AccountNumber[^Math.Min(4, w.AccountNumber.Length)..]}", Status = w.Status, ReferenceId = w.Id.ToString(), CreatedAt = w.CreatedAt });
        foreach (var a in announcements) transactions.Add(new WalletTransactionDto { Id = $"ANN-{a.Id}", Type = "ANNOUNCEMENT_FEE", Amount = -a.BroadcastFee, Description = $"Phí thông báo khui kiện: {a.Title}", Status = "COMPLETED", ReferenceId = a.Id.ToString(), CreatedAt = a.CreatedAt });
        var net = mature - pending - approved - announcements.Sum(a => a.BroadcastFee);
        return new WalletResponse { AvailableBalance = Math.Max(0, net), DeficitAmount = Math.Max(0, -net), PendingReleaseBalance = awaiting, EscrowHoldingBalance = holding, PendingWithdrawalAmount = pending, TotalRevenue = revenue, Transactions = transactions.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).ToList() };
    }
    public async Task<WithdrawalResponse> CreateWithdrawalRequestAsync(int userId, CreateWithdrawalRequest request)
    {
        var bank = request.BankName?.Trim() ?? ""; var account = request.AccountNumber?.Trim() ?? ""; var holder = request.AccountHolder?.Trim().ToUpperInvariant() ?? "";
        if (bank.Length is < 1 or > 100 || holder.Length is < 1 or > 100 || account.Length is < 1 or > 50 || !account.All(char.IsAsciiDigit) || request.Amount is < 50000 or > 100000000 || decimal.Truncate(request.Amount) != request.Amount || !request.ClientRequestId.HasValue || request.ClientRequestId == Guid.Empty)
            throw new ArgumentException("Kiểm tra ngân hàng, chủ tài khoản, số tài khoản chỉ gồm chữ số, mã yêu cầu và số tiền nguyên từ 50.000 đến 100.000.000 VND.");
        await using var tx = await _context.Database.BeginTransactionAsync();
        await WalletRules.LockAsync(_context, userId); await WalletRules.EnsureSellerAsync(_context, userId);
        var existing = await _context.WithdrawalRequests.AsNoTracking().SingleOrDefaultAsync(w => w.UserId == userId && w.ClientRequestId == request.ClientRequestId);
        if (existing != null)
        {
            if (existing.Amount != request.Amount || existing.BankName != bank || existing.AccountNumber != account || existing.AccountHolder != holder) throw new ServiceException(409, "Mã yêu cầu đã dùng với nội dung khác.", "IDEMPOTENCY_CONFLICT");
            await tx.CommitAsync(); return Map(existing);
        }
        if (await _context.WithdrawalRequests.AnyAsync(w => w.UserId == userId && w.Status == WithdrawalStatus.Pending)) throw new ServiceException(409, "Bạn đã có yêu cầu rút đang chờ xử lý. Hãy tải lại lịch sử.", "PENDING_WITHDRAWAL");
        var wallet = await CalculateAsync(_context, userId);
        if (request.Amount > wallet.AvailableBalance) throw new ServiceException(409, "Số dư khả dụng không đủ. Hãy tải lại ví.", "INSUFFICIENT_FUNDS");
        var w = new WithdrawalRequest { UserId = userId, BankName = bank, AccountNumber = account, AccountHolder = holder, Amount = request.Amount, ClientRequestId = request.ClientRequestId, CreatedAt = DateTime.UtcNow, Status = WithdrawalStatus.Pending };
        _context.WithdrawalRequests.Add(w); await _context.SaveChangesAsync(); await tx.CommitAsync();
        _logger.LogInformation("Withdrawal {Id} created for seller {UserId}", w.Id, userId);
        return Map(w);
    }
    public async Task<List<WithdrawalResponse>> GetMyWithdrawalRequestsAsync(int userId)
    {
        await WalletRules.EnsureSellerAsync(_context, userId);
        return (await _context.WithdrawalRequests.AsNoTracking().Where(w => w.UserId == userId).OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id).ToListAsync()).Select(Map).ToList();
    }
    internal static WithdrawalResponse Map(WithdrawalRequest w) => new() { Id = w.Id, UserId = w.UserId, BankName = w.BankName, AccountNumber = w.AccountNumber, AccountHolder = w.AccountHolder, Amount = w.Amount, Status = w.Status, AdminNote = w.AdminNote, CreatedAt = w.CreatedAt, ProcessedAt = w.ProcessedAt, ClientRequestId = w.ClientRequestId };
}
