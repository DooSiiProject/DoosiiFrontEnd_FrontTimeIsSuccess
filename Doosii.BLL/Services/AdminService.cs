using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;

namespace Doosii.BLL.Services
{
    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;
        private readonly IProductLockService _productLockService;
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            AppDbContext context,
            IProductLockService productLockService,
            ILogger<AdminService> logger)
        {
            _context = context;
            _productLockService = productLockService;
            _logger = logger;
        }

        public async Task<List<MerchantProfileDto>> GetKycRequestsAsync(string? status)
        {
            var query = _context.MerchantProfiles
                .Include(m => m.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(m => m.KycStatus.ToUpper() == status.Trim().ToUpper());
            }

            var list = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();

            return list.Select(m => new MerchantProfileDto
            {
                Id = m.Id,
                UserId = m.UserId,
                StoreName = m.StoreName,
                Phone = m.Phone,
                Address = m.Address,
                Latitude = m.Latitude,
                Longitude = m.Longitude,
                KycStatus = m.KycStatus,
                LicenseImageUrl = m.LicenseImageUrl,
                FrontFacadeUrl = m.FrontFacadeUrl,
                IdCardFrontUrl = m.IdCardFrontUrl,
                IdCardBackUrl = m.IdCardBackUrl,
                RejectionReason = m.RejectionReason,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            }).ToList();
        }

        public async Task<MerchantProfileDto> ReviewKycRequestAsync(int profileId, ReviewKycRequest request)
        {
            var profile = await _context.MerchantProfiles
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == profileId);

            if (profile == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy hồ sơ đăng ký KYC #{profileId}.");
            }

            if (request.IsApproved)
            {
                profile.KycStatus = "APPROVED";
                profile.RejectionReason = null;
                profile.User.Role = "Seller"; // Nâng cấp vai trò người dùng thành Người bán (Seller)
            }
            else
            {
                profile.KycStatus = "REJECTED";
                profile.RejectionReason = request.RejectionReason?.Trim() ?? "Không đạt yêu cầu xác thực người bán.";
            }

            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin reviewed KYC #{ProfileId} (User #{UserId}): Status = {Status}",
                profile.Id, profile.UserId, profile.KycStatus);

            return new MerchantProfileDto
            {
                Id = profile.Id,
                UserId = profile.UserId,
                StoreName = profile.StoreName,
                Phone = profile.Phone,
                Address = profile.Address,
                Latitude = profile.Latitude,
                Longitude = profile.Longitude,
                KycStatus = profile.KycStatus,
                LicenseImageUrl = profile.LicenseImageUrl,
                FrontFacadeUrl = profile.FrontFacadeUrl,
                IdCardFrontUrl = profile.IdCardFrontUrl,
                IdCardBackUrl = profile.IdCardBackUrl,
                RejectionReason = profile.RejectionReason,
                CreatedAt = profile.CreatedAt,
                UpdatedAt = profile.UpdatedAt
            };
        }

        public async Task<List<DisputeResponse>> GetDisputesAsync(string? status)
        {
            var query = _context.Disputes
                .Include(d => d.Order)
                .Include(d => d.RaisedByUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(d => d.Status.ToUpper() == status.Trim().ToUpper());
            }

            var list = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();

            return list.Select(d => MapToDisputeResponse(d, d.RaisedByUser?.FullName ?? "Unknown")).ToList();
        }

        public async Task<DisputeResponse> ArbitrateDisputeAsync(int disputeId, ArbitrateDisputeRequest request)
        {
            var dispute = await _context.Disputes
                .Include(d => d.RaisedByUser)
                .Include(d => d.Order)
                    .ThenInclude(o => o.Items)
                .Include(d => d.Order)
                    .ThenInclude(o => o.EscrowTransactions)
                .FirstOrDefaultAsync(d => d.Id == disputeId);

            if (dispute == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy khiếu nại #{disputeId}.");
            }

            if (dispute.Status != DisputeStatus.PendingReview)
            {
                throw new InvalidOperationException($"Khiếu nại này đã được phân xử trước đó (Trạng thái: {dispute.Status}).");
            }

            var order = dispute.Order;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (request.Verdict.Equals("REFUND_BUYER", StringComparison.OrdinalIgnoreCase))
                {
                    // Phán quyết: Hoàn tiền 100% cho người mua
                    dispute.Status = DisputeStatus.RefundedBuyer;
                    dispute.AdminVerdict = request.AdminVerdict.Trim();
                    dispute.ResolvedAt = DateTime.UtcNow;

                    order.Status = OrderStatus.Refunded;
                    order.UpdatedAt = DateTime.UtcNow;

                    // Cập nhật trạng thái quỹ ký quỹ sang REFUNDED
                    foreach (var escrow in order.EscrowTransactions.Where(e => e.Status == EscrowStatus.Holding))
                    {
                        escrow.Status = EscrowStatus.Refunded;
                    }

                    // Mở khóa lại các sản phẩm về AVAILABLE
                    foreach (var item in order.Items)
                    {
                        await _productLockService.UnlockProductAsync(item.ProductId);
                    }
                }
                else if (request.Verdict.Equals("RELEASE_SELLER", StringComparison.OrdinalIgnoreCase))
                {
                    // Phán quyết: Bác khiếu nại, giải ngân tiền cho người bán
                    dispute.Status = DisputeStatus.ReleasedSeller;
                    dispute.AdminVerdict = request.AdminVerdict.Trim();
                    dispute.ResolvedAt = DateTime.UtcNow;

                    order.Status = OrderStatus.CompletedReleased;
                    order.CompletedAt = DateTime.UtcNow;
                    order.UpdatedAt = DateTime.UtcNow;

                    // Giải ngân quỹ ký quỹ
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

                    // Đánh dấu sản phẩm là SOLD vĩnh viễn
                    foreach (var item in order.Items)
                    {
                        await _productLockService.MarkProductAsSoldAsync(item.ProductId);
                    }
                }
                else
                {
                    throw new ArgumentException("Phán quyết không hợp lệ. Phải là REFUND_BUYER hoặc RELEASE_SELLER.");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Admin arbitrated Dispute #{DisputeId} (Order #{OrderId}): Verdict = {Verdict}",
                    dispute.Id, order.Id, dispute.Status);

                return MapToDisputeResponse(dispute, dispute.RaisedByUser?.FullName ?? "Unknown");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi Admin phân xử khiếu nại #{DisputeId}", disputeId);
                throw;
            }
        }

        public async Task<List<WithdrawalResponse>> GetWithdrawalsAsync(string? status)
        {
            var query = _context.WithdrawalRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(w => w.Status.ToUpper() == status.Trim().ToUpper());
            }

            var list = await query.OrderByDescending(w => w.CreatedAt).ToListAsync();

            return list.Select(w => new WithdrawalResponse
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
            }).ToList();
        }

        public async Task<WithdrawalResponse> ProcessWithdrawalAsync(int withdrawalId, ProcessWithdrawalRequest request)
        {
            var withdrawal = await _context.WithdrawalRequests.FindAsync(withdrawalId);
            if (withdrawal == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy yêu cầu rút tiền #{withdrawalId}.");
            }

            if (withdrawal.Status != WithdrawalStatus.Pending)
            {
                throw new InvalidOperationException($"Yêu cầu rút tiền này đã được xử lý trước đó (Trạng thái: {withdrawal.Status}).");
            }

            withdrawal.Status = request.IsApproved ? WithdrawalStatus.Approved : WithdrawalStatus.Rejected;
            withdrawal.AdminNote = request.AdminNote?.Trim();
            withdrawal.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin processed Withdrawal #{WithdrawalId} (User #{UserId}): Status = {Status}",
                withdrawal.Id, withdrawal.UserId, withdrawal.Status);

            return new WithdrawalResponse
            {
                Id = withdrawal.Id,
                UserId = withdrawal.UserId,
                BankName = withdrawal.BankName,
                AccountNumber = withdrawal.AccountNumber,
                AccountHolder = withdrawal.AccountHolder,
                Amount = withdrawal.Amount,
                Status = withdrawal.Status,
                AdminNote = withdrawal.AdminNote,
                CreatedAt = withdrawal.CreatedAt,
                ProcessedAt = withdrawal.ProcessedAt
            };
        }

        public async Task<AdminAnalyticsResponse> GetAnalyticsAsync()
        {
            // 1. GMV: Tổng giá trị các đơn hàng đã thanh toán và vận hành
            var paidStatuses = new[]
            {
                OrderStatus.EscrowHolding,
                OrderStatus.InTransit,
                OrderStatus.CompletedReleased,
                OrderStatus.Disputed
            };

            var totalGmv = await _context.Orders
                .Where(o => paidStatuses.Contains(o.Status))
                .SumAsync(o => o.TotalAmount);

            // 2. Doanh thu phí sàn: 2.5% từ các đơn đã hoàn tất
            var escrowRevenue = await _context.Orders
                .Where(o => o.Status == OrderStatus.CompletedReleased)
                .SumAsync(o => o.EscrowFee);

            // 3. Số lượng đơn hàng
            var totalOrders = await _context.Orders.CountAsync();
            var completedOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.CompletedReleased);
            var disputedOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Disputed || o.Dispute != null);

            // 4. Số lượng tài khoản
            var totalSellers = await _context.Users.CountAsync(u => u.Role == "Seller");
            var totalCustomers = await _context.Users.CountAsync(u => u.Role == "Customer" || u.Role == "User");

            return new AdminAnalyticsResponse
            {
                TotalGmv = totalGmv,
                PlatformEscrowRevenue = escrowRevenue,
                TotalOrders = totalOrders,
                CompletedOrders = completedOrders,
                DisputedOrders = disputedOrders,
                TotalVerifiedSellers = totalSellers,
                TotalCustomers = totalCustomers
            };
        }

        private static DisputeResponse MapToDisputeResponse(Dispute dispute, string buyerName)
        {
            var images = new List<string>();
            if (!string.IsNullOrWhiteSpace(dispute.EvidenceImagesJson))
            {
                try
                {
                    images = JsonSerializer.Deserialize<List<string>>(dispute.EvidenceImagesJson) ?? new List<string>();
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
    }
}
