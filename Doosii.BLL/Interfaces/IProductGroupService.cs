using Doosii.BLL.DTOs;
namespace Doosii.BLL.Interfaces;

public interface IProductGroupService
{
    Task<ProductGroupWriteResult> CreateAsync(int userId, int storeId, CreateProductGroupRequest request);
    Task<ProductGroupWriteResult> UpdateAsync(int userId, int groupId, UpdateProductGroupRequest request);
    Task<ProductGroupManageDto> ManageAsync(int userId, int groupId);
    Task<PagedResult<ProductGroupManageDto>> MineAsync(int userId, int page, int pageSize);
    Task<PagedResult<ProductGroupSummaryDto>> StoreAsync(int storeId, int page, int pageSize);
    Task<ProductGroupDetailDto> DetailAsync(int groupId);
}
