using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IStoreReviewService
    {
        Task<StoreReviewDto> CreateReviewAsync(int userId, int storeId, CreateStoreReviewRequest request);
        Task<StoreReviewDto> UpdateReviewAsync(int userId, int storeId, int reviewId, UpdateStoreReviewRequest request);
        Task<StoreReviewsSummaryDto> GetReviewsAsync(int storeId, StoreReviewQuery query);
        Task DeleteReviewAsync(int userId, bool isAdmin, int storeId, int reviewId);
    }
}
