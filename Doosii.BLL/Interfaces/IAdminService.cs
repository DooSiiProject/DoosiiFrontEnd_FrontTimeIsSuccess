using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IAdminService
    {
        Task<List<MerchantProfileDto>> GetKycRequestsAsync(string? status);
        Task<MerchantProfileDto> GetKycRequestByIdAsync(int profileId);
        Task<MerchantProfileDto> ReviewKycRequestAsync(int profileId, ReviewKycRequest request);
        Task<MerchantProfileDto> ApproveKycRequestAsync(int profileId, ApproveKycRequest request);
        Task<MerchantProfileDto> RejectKycRequestAsync(int profileId, string? reason);
        Task<PagedResult<DisputeResponse>> GetDisputesAsync(string? status, int page = 1, int pageSize = 20);
        Task<DisputeResponse> ArbitrateDisputeAsync(int disputeId, ArbitrateDisputeRequest request);
        Task<PagedResult<WithdrawalResponse>> GetWithdrawalsAsync(string? status, int page = 1, int pageSize = 20);
        Task<WithdrawalResponse> ProcessWithdrawalAsync(int withdrawalId, ProcessWithdrawalRequest request, int actorId);
        Task<AdminAnalyticsResponse> GetAnalyticsAsync();
    }
}
