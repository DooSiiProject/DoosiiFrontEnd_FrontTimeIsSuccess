using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IProductService
    {
        Task<ProductDto> CreateProductAsync(int userId, int storeId, CreateProductRequest request);
        Task<ProductDto> UpdateProductAsync(int userId, int productId, UpdateProductRequest request);
        Task DeleteProductAsync(int userId, int productId);
        Task<PagedResult<ProductDto>> GetStoreProductsAsync(int storeId, int page, int pageSize);
    }
}