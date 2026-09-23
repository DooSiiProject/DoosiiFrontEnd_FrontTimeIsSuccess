using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IAdminService
    {
        Task<List<MerchantProfileDto>> GetKycRequestsAsync(string? status);
        Task<MerchantProfileDto> ReviewKycRequestAsync(int profileId, ReviewKycRequest request);
        Task<List<DisputeResponse>> GetDisputesAsync(string? status);
        Task<DisputeResponse> ArbitrateDisputeAsync(int disputeId, ArbitrateDisputeRequest request);
        Task<List<WithdrawalResponse>> GetWithdrawalsAsync(string? status);
        Task<WithdrawalResponse> ProcessWithdrawalAsync(int withdrawalId, ProcessWithdrawalRequest request);
        Task<AdminAnalyticsResponse> GetAnalyticsAsync();
    }
}
