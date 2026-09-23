using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;

namespace Doosii.BLL.Services
{
    public class WalletService : IWalletService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WalletService> _logger;

        public WalletService(AppDbContext context, ILogger<WalletService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<WalletResponse> GetSellerWalletAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản người dùng.");
            }

            // 1. Lấy tất cả đơn bán của người dùng
            var salesOrders = await _context.Orders
                .Where(o => o.SellerId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // 2. Lấy tất cả các yêu cầu rút tiền của người dùng
            var withdrawalRequests = await _context.WithdrawalRequests
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            // Tính toán doanh thu đã giải ngân (COMPLETED_RELEASED)
            decimal totalReleasedRevenue = 0;
            decimal escrowHoldingBalance = 0;

            var transactions = new List<WalletTransactionDto>();

            foreach (var order in salesOrders)
            {
                // Tiền người bán nhận được sau khi trừ phí ký quỹ 2.5%
                var sellerPayout = order.ProductPrice + order.ShippingFee - order.EscrowFee;

                if (order.Status == OrderStatus.CompletedReleased)
                {
                    totalReleasedRevenue += sellerPayout;

                    transactions.Add(new WalletTransactionDto
                    {
                        Id = $"ORD-{order.Id}",
                        Type = "ORDER_PAYOUT",
                        Amount = sellerPayout,
                        Description = $"Tiền bán hàng đơn #{order.Id} (Đã trừ phí sàn)",
                        Status = "COMPLETED",
                        ReferenceId = order.Id.ToString(),
                        CreatedAt = order.CompletedAt ?? order.UpdatedAt ?? order.CreatedAt
                    });
                }
                else if (order.Status == OrderStatus.EscrowHolding || order.Status == OrderStatus.InTransit)
                {
                    escrowHoldingBalance += sellerPayout;
                }
            }

            // Tính tiền đã rút và đang chờ rút
            decimal pendingWithdrawalAmount = 0;
            decimal approvedWithdrawalAmount = 0;

            foreach (var w in withdrawalRequests)
            {
                if (w.Status == WithdrawalStatus.Pending)
                {
                    pendingWithdrawalAmount += w.Amount;
                }
                else if (w.Status == WithdrawalStatus.Approved)
                {
                    approvedWithdrawalAmount += w.Amount;
                }

                transactions.Add(new WalletTransactionDto
                {
                    Id = $"WDR-{w.Id}",
                    Type = "WITHDRAWAL",
                    Amount = -w.Amount, // Ghi nợ số âm
                    Description = $"Rút tiền về {w.BankName} - {w.AccountNumber} ({w.AccountHolder})",
                    Status = w.Status,
                    ReferenceId = w.Id.ToString(),
                    CreatedAt = w.CreatedAt
                });
            }

            // Số dư khả dụng = Tổng tiền đã giải ngân - (Đã duyệt rút + Đang chờ duyệt rút)
            var availableBalance = totalReleasedRevenue - (approvedWithdrawalAmount + pendingWithdrawalAmount);
            if (availableBalance < 0) availableBalance = 0;

            return new WalletResponse
            {
                AvailableBalance = availableBalance,
                EscrowHoldingBalance = escrowHoldingBalance,
                PendingWithdrawalAmount = pendingWithdrawalAmount,
                TotalRevenue = totalReleasedRevenue,
                Transactions = transactions.OrderByDescending(t => t.CreatedAt).ToList()
            };
        }

        public async Task<WithdrawalResponse> CreateWithdrawalRequestAsync(int userId, CreateWithdrawalRequest request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản người dùng.");
            }

            // 1. Kiểm tra xem người dùng có yêu cầu nào đang PENDING không (REQ-19)
            var hasPending = await _context.WithdrawalRequests
                .AnyAsync(w => w.UserId == userId && w.Status == WithdrawalStatus.Pending);

            if (hasPending)
            {
                throw new InvalidOperationException("Bạn hiện đang có một yêu cầu rút tiền đang chờ xử lý. Vui lòng đợi hoàn tất trước khi tạo yêu cầu mới.");
            }

            // 2. Tính số dư khả dụng hiện tại
            var wallet = await GetSellerWalletAsync(userId);
            if (request.Amount > wallet.AvailableBalance)
            {
                throw new InvalidOperationException($"Số dư khả dụng ({wallet.AvailableBalance:N0} VND) không đủ để thực hiện rút {request.Amount:N0} VND.");
            }

            // 3. Tạo bản ghi WithdrawalRequest
            var withdrawal = new WithdrawalRequest
            {
                UserId = userId,
                BankName = request.BankName.Trim(),
                AccountNumber = request.AccountNumber.Trim(),
                AccountHolder = request.AccountHolder.Trim().ToUpperInvariant(),
                Amount = request.Amount,
                Status = WithdrawalStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.WithdrawalRequests.Add(withdrawal);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User #{UserId} created withdrawal request #{WithdrawalId} for {Amount:N0} VND to {BankName} {AccountNumber}",
                userId, withdrawal.Id, withdrawal.Amount, withdrawal.BankName, withdrawal.AccountNumber);

            return MapToWithdrawalResponse(withdrawal);
        }

        public async Task<List<WithdrawalResponse>> GetMyWithdrawalRequestsAsync(int userId)
        {
            var list = await _context.WithdrawalRequests
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            return list.Select(MapToWithdrawalResponse).ToList();
        }

        private static WithdrawalResponse MapToWithdrawalResponse(WithdrawalRequest w)
        {
            return new WithdrawalResponse
            {
                Id = w.Id,
                UserId = w.UserId,
                BankName = w.BankName,
                AccountNumber = w.AccountNumber,
                AccountHolder = w.AccountHolder,
                Amount = w.Amount,
                Status = w.Status,
                AdminNote = w.AdminNote,
                CreatedAt = w.CreatedAt,
                ProcessedAt = w.ProcessedAt
            };
        }
    }
}
