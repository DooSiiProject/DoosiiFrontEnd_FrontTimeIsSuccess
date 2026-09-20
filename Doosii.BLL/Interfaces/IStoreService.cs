using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IStoreService
    {
        Task<StoreDto> CreateStoreAsync(int userId, CreateStoreRequest request);
        Task<StoreDto> UpdateStoreAsync(int userId, int storeId, UpdateStoreRequest request);
        Task<StoreDto> GetStoreAsync(int storeId);
    }
}