using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IWalletService
    {
        Task<WalletResponse> GetSellerWalletAsync(int userId);
        Task<WithdrawalResponse> CreateWithdrawalRequestAsync(int userId, CreateWithdrawalRequest request);
        Task<List<WithdrawalResponse>> GetMyWithdrawalRequestsAsync(int userId);
    }
}
