using Doosii.BLL.DTOs;
namespace Doosii.BLL.Interfaces;

public interface IShopRelocationService
{
    Task<ShopRelocationPage> GetAsync(int userId, int storeId, int page, int pageSize);
    Task<ShopRelocationResponse> CreateAsync(int userId, int storeId, CreateShopRelocationRequest request);
    Task<ShopRelocationResponse> CancelAsync(int userId, int storeId, int requestId);
    Task<AdminShopRelocationPage> AdminListAsync(int adminId, string? status, int page, int pageSize);
    Task<AdminShopRelocationResponse> AdminDetailAsync(int adminId, int requestId);
    Task<AdminShopRelocationResponse> ReviewAsync(int adminId, int requestId, ReviewShopRelocationRequest request);
}
