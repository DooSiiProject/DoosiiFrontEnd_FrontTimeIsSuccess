using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IProductService
    {
        Task<PagedResult<ProductCatalogItemDto>> GetCatalogAsync(ProductCatalogQuery query, CancellationToken cancellationToken = default);
        Task<PagedResult<ProductDto>> GetMyProductsAsync(int userId, int page, int pageSize, string? state);
        Task<ProductDto> GetManagedProductAsync(int userId, int productId);
        Task<ProductDto> SetPublicationAsync(int userId, int productId, ProductPublicationRequest request);
        Task<ProductDto> CreateProductAsync(int userId, int storeId, CreateProductRequest request);
        Task<ProductDto> UpdateProductAsync(int userId, int productId, UpdateProductRequest request);
        Task DeleteProductAsync(int userId, int productId);
        Task<PagedResult<ProductDto>> GetStoreProductsAsync(int storeId, int page, int pageSize, int? categoryId = null);
        Task<List<ProductCategoryDto>> GetCategoriesAsync();
        Task<ProductDetailDto> GetProductDetailAsync(int productId);
    }
}

