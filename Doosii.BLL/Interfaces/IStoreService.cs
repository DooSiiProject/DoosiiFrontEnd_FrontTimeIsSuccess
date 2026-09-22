using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IStoreService
    {
        Task<StoreDto> CreateStoreAsync(int userId, CreateStoreRequest request);
        Task<StoreDto> UpdateStoreAsync(int userId, int storeId, UpdateStoreRequest request);
        Task<StoreDto> GetStoreAsync(int storeId);
        Task<PagedResponse<NearbyStoreDto>> GetNearbyStoresAsync(NearbyStoreQuery query);
        Task<PagedResponse<NearbyStoreDto>> GetStoresAsync(StoreFilterQuery query);
    }
}
