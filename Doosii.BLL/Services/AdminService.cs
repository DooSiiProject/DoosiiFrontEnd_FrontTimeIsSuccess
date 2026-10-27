using System.Data;
using Doosii.DAL.Models.Store;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
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

            return list.Select(MapToMerchantProfileDto).ToList();
        }

        public async Task<MerchantProfileDto> GetKycRequestByIdAsync(int profileId)
        {
            var profile = await _context.MerchantProfiles
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == profileId);

            if (profile == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy hồ sơ đăng ký KYC #{profileId}.");
            }

            return MapToMerchantProfileDto(profile);
        }

        public async Task<MerchantProfileDto> ReviewKycRequestAsync(int profileId, ReviewKycRequest request)
        {
            // Serialize approval with other approval/create operations for this owner.
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var profile = await _context.MerchantProfiles
                .FromSqlInterpolated($"SELECT * FROM [auth].[MerchantProfiles] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {profileId}")
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == profileId)
                ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ #{profileId}.");
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.OwnerId == profile.UserId);
            if (request.IsApproved && profile.KycStatus == "APPROVED" && store != null)
            {
                await transaction.CommitAsync();
                return MapToMerchantProfileDto(profile);
            }
            if (profile.KycStatus != "PENDING")
                throw new InvalidOperationException("Hồ sơ đã được xét duyệt. Vui lòng tải lại trang.");
            if (request.IsApproved)
            {
                if (!request.LocationConfirmed || !request.Latitude.HasValue || !request.Longitude.HasValue ||
                    !double.IsFinite(request.Latitude.Value) || !double.IsFinite(request.Longitude.Value) ||
                    request.Latitude < -90 || request.Latitude > 90 || request.Longitude < -180 || request.Longitude > 180)
                    throw new ArgumentException("Cần xác nhận tọa độ hợp lệ trước khi duyệt.");
                if (store != null) throw new InvalidOperationException("Tài khoản đã có shop.");
                if (string.IsNullOrWhiteSpace(profile.Address)) throw new ArgumentException("Hồ sơ cần có địa chỉ kinh doanh.");
                profile.Latitude = request.Latitude;
                profile.Longitude = request.Longitude;
                profile.KycStatus = "APPROVED";
                profile.RejectionReason = null;
                profile.User.Role = "Shopper";
                profile.User.UpdatedAt = DateTime.UtcNow;
                store = new Store { OwnerId = profile.UserId, Name = profile.StoreName,
                    Phone = profile.Phone, Address = profile.Address, Latitude = profile.Latitude,
                    Longitude = profile.Longitude, IsActive = true, CreatedAt = DateTime.UtcNow };
                store.Locations.Add(new StoreLocation { Address = profile.Address,
                    Latitude = profile.Latitude, Longitude = profile.Longitude });
                _context.Stores.Add(store);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.RejectionReason)) throw new ArgumentException("Vui lòng nhập lý do từ chối.");
                profile.KycStatus = "REJECTED";
                profile.RejectionReason = request.RejectionReason.Trim();
            }
            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation("KYC {ProfileId} reviewed: {Status}", profile.Id, profile.KycStatus);
            return MapToMerchantProfileDto(profile);
        }

        public Task<MerchantProfileDto> ApproveKycRequestAsync(int profileId, ApproveKycRequest request)
            => ReviewKycRequestAsync(profileId, new ReviewKycRequest { IsApproved = true,
                Latitude = request.Latitude, Longitude = request.Longitude, LocationConfirmed = request.LocationConfirmed });

        public Task<MerchantProfileDto> RejectKycRequestAsync(int profileId, string? reason)
            => ReviewKycRequestAsync(profileId, new ReviewKycRequest { IsApproved = false, RejectionReason = reason });

        private static MerchantProfileDto MapToMerchantProfileDto(MerchantProfile m)
        {
            List<string> mediaUrls = new List<string>();
            if (!string.IsNullOrWhiteSpace(m.ShopMediaUrls))
            {
                try
                {
                    mediaUrls = JsonSerializer.Deserialize<List<string>>(m.ShopMediaUrls) ?? new List<string>();
                }
                catch
                {
                    mediaUrls = new List<string>();
                }
            }
            else if (!string.IsNullOrWhiteSpace(m.FrontFacadeUrl))
            {
                mediaUrls.Add(m.FrontFacadeUrl);
            }

            return new MerchantProfileDto
            {
                Id = m.Id,
                UserId = m.UserId,
                ContactName = m.ContactName ?? m.User?.FullName,
                ContactEmail = m.ContactEmail ?? m.User?.Email,
                StoreName = m.StoreName,
                Phone = m.Phone,
                Address = m.Address,
                AddressType = m.AddressType ?? "NEW",
                Latitude = m.Latitude,
                Longitude = m.Longitude,
                EstablishedDate = m.EstablishedDate,
                TaxCode = m.TaxCode,
                ShopMediaUrls = mediaUrls,
                KycStatus = m.KycStatus,
                LicenseImageUrl = m.LicenseImageUrl,
                FrontFacadeUrl = m.FrontFacadeUrl,
                IdCardFrontUrl = m.IdCardFrontUrl,
                IdCardBackUrl = m.IdCardBackUrl,
                RejectionReason = m.RejectionReason,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            };
        }

        public async Task<PagedResult<DisputeResponse>> GetDisputesAsync(string? status, int page = 1, int pageSize = 20)
        {
            if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentException("Phân trang không hợp lệ.");
            var query = _context.Disputes.AsNoTracking().Include(d => d.RaisedByUser).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
            {
                status = status.Trim().ToUpperInvariant();
                if (!new[] { DisputeStatus.PendingReview, DisputeStatus.RefundedBuyer, DisputeStatus.ReleasedSeller }.Contains(status)) throw new ArgumentException("Trạng thái không hợp lệ.");
                query = query.Where(d => d.Status == status);
            }
            var count = await query.CountAsync();
            var rows = await query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new() { Items = rows.Select(d => MapToDisputeResponse(d, d.RaisedByUser.FullName)).ToList(), TotalCount = count, Page = page, PageSize = pageSize };
        }

        public async Task<DisputeResponse> ArbitrateDisputeAsync(int disputeId, ArbitrateDisputeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AdminVerdict) || request.AdminVerdict.Trim().Length > 1000 || !new[] { "REFUND_BUYER", "RELEASE_SELLER" }.Contains(request.Verdict))
                throw new ArgumentException("Chọn quyết định hợp lệ và giải trình từ 1 đến 1000 ký tự.");
            var orderId = await _context.Disputes.AsNoTracking().Where(d => d.Id == disputeId).Select(d => (int?)d.OrderId).SingleOrDefaultAsync()
                ?? throw new KeyNotFoundException("Không tìm thấy khiếu nại.");
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var order = await OrderLifecycle.LockAsync(_context, orderId);
            var dispute = order.Dispute ?? throw new KeyNotFoundException("Không tìm thấy khiếu nại.");
            if (dispute.Id != disputeId || dispute.Status != DisputeStatus.PendingReview || order.Status != OrderStatus.Disputed)
                throw new InvalidOperationException("Khiếu nại hoặc đơn hàng đã được xử lý. Vui lòng tải lại.");
            var funds = order.EscrowTransactions.Where(e => e.Status == EscrowStatus.Holding || e.Status == EscrowStatus.Released).ToList();
            if (funds.Count != 1) throw new InvalidOperationException("Không xác định được khoản ký quỹ. Chưa thể phân xử.");
            var escrow = funds[0];
            var now = DateTime.UtcNow;
            if (request.Verdict == "REFUND_BUYER")
            {
                dispute.Status = DisputeStatus.RefundedBuyer;
                order.Status = OrderStatus.Refunded;
                escrow.Status = EscrowStatus.Refunded;
                foreach (var item in order.Items) await _productLockService.UnlockProductAsync(item.ProductId, order.Id);
            }
            else
            {
                dispute.Status = DisputeStatus.ReleasedSeller;
                order.Status = OrderStatus.CompletedReleased;
                order.CompletedAt ??= now;
                if (escrow.Status == EscrowStatus.Holding) { escrow.Status = EscrowStatus.Released; escrow.ReleasedAt = now; }
                foreach (var item in order.Items) await _productLockService.MarkProductAsSoldAsync(item.ProductId, order.Id);
            }
            dispute.AdminVerdict = request.AdminVerdict.Trim(); dispute.ResolvedAt = now; order.UpdatedAt = now;
            await _context.SaveChangesAsync(); await transaction.CommitAsync();
            _logger.LogInformation("Dispute {DisputeId} of order {OrderId} resolved as {Status}", dispute.Id, order.Id, dispute.Status);
            return MapToDisputeResponse(dispute, order.Buyer.FullName);
        }

        public async Task<PagedResult<WithdrawalResponse>> GetWithdrawalsAsync(string? status, int page = 1, int pageSize = 20)
        {
            var filter = status?.Trim().ToUpperInvariant();
            if (page < 1 || page > 1000000 || pageSize < 1 || pageSize > 50 || !string.IsNullOrEmpty(filter) && filter is not ("PENDING" or "APPROVED" or "REJECTED")) throw new ArgumentException("Bộ lọc hoặc phân trang không hợp lệ.");
            var query = _context.WithdrawalRequests.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(filter)) query = query.Where(w => w.Status == filter);
            var count = await query.CountAsync();
            var items = await query.OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PagedResult<WithdrawalResponse> { Items = items.Select(WalletService.Map).ToList(), TotalCount = count, Page = page, PageSize = pageSize };
        }

        public async Task<WithdrawalResponse> ProcessWithdrawalAsync(int withdrawalId, ProcessWithdrawalRequest request, int actorId)
        {
            if (!request.IsApproved.HasValue || string.IsNullOrWhiteSpace(request.AdminNote) || request.AdminNote.Trim().Length > 500) throw new ArgumentException("Chọn quyết định và nhập giải trình tối đa 500 ký tự.");
            await using var tx = await _context.Database.BeginTransactionAsync();
            if (!await _context.Users.AsNoTracking().AnyAsync(u => u.Id == actorId && u.Role == "Admin")) throw new Doosii.BLL.Exceptions.ServiceException(403, "Chỉ quản trị viên hiện tại được xử lý.", "ADMIN_REQUIRED");
            var sellerId = await _context.WithdrawalRequests.AsNoTracking().Where(w => w.Id == withdrawalId).Select(w => (int?)w.UserId).SingleOrDefaultAsync() ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu rút tiền.");
            await WalletRules.LockAsync(_context, sellerId);
            var withdrawal = await _context.WithdrawalRequests.SingleAsync(w => w.Id == withdrawalId);
            await _context.Entry(withdrawal).ReloadAsync();
            if (withdrawal.Status != WithdrawalStatus.Pending) throw new Doosii.BLL.Exceptions.ServiceException(409, "Yêu cầu đã được xử lý. Hãy tải lại danh sách.", "ALREADY_PROCESSED");
            if (actorId == sellerId) throw new Doosii.BLL.Exceptions.ServiceException(403, "Không được tự xử lý yêu cầu rút của mình.", "SELF_REVIEW");
            if (request.IsApproved.Value)
            {
                await WalletRules.EnsureSellerAsync(_context, sellerId);
                var wallet = await WalletService.CalculateAsync(_context, sellerId);
                if (wallet.DeficitAmount > 0) throw new Doosii.BLL.Exceptions.ServiceException(409, "Số dư không còn đủ để duyệt. Cần từ chối yêu cầu hoặc đối soát.", "INSUFFICIENT_FUNDS");
            }
            withdrawal.Status = request.IsApproved.Value ? WithdrawalStatus.Approved : WithdrawalStatus.Rejected;
            withdrawal.AdminNote = request.AdminNote.Trim(); withdrawal.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(); await tx.CommitAsync();
            _logger.LogInformation("Admin {ActorId} processed withdrawal {WithdrawalId}: {Status}", actorId, withdrawalId, withdrawal.Status);
            return WalletService.Map(withdrawal);
        }

        public async Task<AdminAnalyticsResponse> GetAnalyticsAsync()
        {
            // Group once so the order counters and status distribution use the same query result.
            var orders = await _context.Orders.AsNoTracking().GroupBy(o => o.Status).Select(g => new {
                Status = g.Key, Count = g.Count(), ProductValue = g.Sum(o => o.ProductPrice),
                PaidValue = g.Sum(o => o.TotalAmount), Fee = g.Sum(o => o.EscrowFee),
                WithDispute = g.Count(o => o.Dispute != null)
            }).ToListAsync();
            var paid = orders.Where(g => g.Status is OrderStatus.EscrowHolding or OrderStatus.InTransit or OrderStatus.CompletedReleased or OrderStatus.CodCompleted or OrderStatus.Disputed).ToList();
            var holding = await _context.EscrowTransactions.AsNoTracking().Where(e => e.Status == EscrowStatus.Holding).SumAsync(e => (decimal?)e.Amount) ?? 0;
            var pendingDisputes = await _context.Disputes.AsNoTracking().CountAsync(d => d.Status == "PENDING_REVIEW");
            var pendingWithdrawals = await _context.WithdrawalRequests.AsNoTracking().Where(w => w.Status == WithdrawalStatus.Pending).GroupBy(w => 1).Select(g => new { Count = g.Count(), Amount = g.Sum(w => w.Amount) }).SingleOrDefaultAsync();
            var users = await _context.Users.AsNoTracking().GroupBy(u => u.Role).Select(g => new { Role = g.Key, Count = g.Count(), Verified = g.Count(u => u.MerchantProfile != null && u.MerchantProfile.KycStatus == "APPROVED") }).ToListAsync();
            var activeStores = await _context.Stores.AsNoTracking().CountAsync(s => s.IsActive && s.Owner.Role == "Shopper" && s.Owner.MerchantProfile != null && s.Owner.MerchantProfile.KycStatus == "APPROVED");
            return new AdminAnalyticsResponse {
                TotalGmv = paid.Sum(g => g.ProductValue), TotalPaidAmount = paid.Sum(g => g.PaidValue),
                PlatformEscrowRevenue = orders.Where(g => g.Status == OrderStatus.CompletedReleased).Sum(g => g.Fee),
                EscrowHoldingBalance = holding, TotalOrders = orders.Sum(g => g.Count),
                CompletedOrders = orders.Where(g => g.Status == OrderStatus.CompletedReleased || g.Status == OrderStatus.CodCompleted).Sum(g => g.Count),
                DisputedOrders = orders.Sum(g => g.Status == OrderStatus.Disputed ? g.Count : g.WithDispute),
                PendingDisputes = pendingDisputes, PendingWithdrawalCount = pendingWithdrawals?.Count ?? 0,
                PendingWithdrawalAmount = pendingWithdrawals?.Amount ?? 0,
                TotalVerifiedSellers = users.Where(g => g.Role == "Shopper").Sum(g => g.Verified),
                TotalCustomers = users.Where(g => g.Role is "CasualUser" or "User").Sum(g => g.Count),
                TotalActiveStores = activeStores, GeneratedAt = DateTime.UtcNow,
                OrderStatusCounts = orders.OrderBy(g => g.Status).Select(g => new AnalyticsOrderStatus { Status = g.Status, Count = g.Count }).ToList()
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
