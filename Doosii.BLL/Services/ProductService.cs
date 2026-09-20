using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Store;

namespace Doosii.BLL.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;

        public ProductService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ProductDto> CreateProductAsync(int userId, int storeId, CreateProductRequest request)
        {
            var store = await _context.Stores
                .Include(s => s.Owner)
                .ThenInclude(o => o.MerchantProfile)
                .FirstOrDefaultAsync(s => s.Id == storeId && s.IsActive);

            if (store == null)
                throw new KeyNotFoundException("Khong tim thay cua hang hoac cua hang khong hoat dong.");

            if (store.OwnerId != userId)
                throw new UnauthorizedAccessException("Ban khong phai chu so huu cua hang nay.");

            if (store.Owner.Role != "Seller" || store.Owner.MerchantProfile?.KycStatus != "APPROVED")
                throw new UnauthorizedAccessException("Tai khoan chua duoc duyet KYC.");

            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId);
            if (!categoryExists)
                throw new KeyNotFoundException("Khong tim thay danh muc san pham.");

            if (request.ImageUrls == null || request.ImageUrls.Count < 1 || request.ImageUrls.Count > 5)
                throw new ArgumentException("San pham can tu 1 den 5 anh.");

            var product = new Product
            {
                StoreId = storeId,
                CategoryId = request.CategoryId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                Price = request.Price,
                Size = request.Size?.Trim(),
                ConditionPercent = request.ConditionPercent,
                Status = "AVAILABLE",
                StyleTags = request.StyleTags?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var images = request.ImageUrls
                .Select((url, index) => new ProductImage
                {
                    ProductId = product.Id,
                    ImageUrl = url.Trim(),
                    DisplayOrder = index
                }).ToList();

            _context.ProductImages.AddRange(images);
            await _context.SaveChangesAsync();

            return await GetProductDtoAsync(product.Id);
        }

        public async Task<ProductDto> UpdateProductAsync(int userId, int productId, UpdateProductRequest request)
        {
            var product = await _context.Products
                .Include(p => p.Store)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                throw new KeyNotFoundException("Khong tim thay san pham.");

            if (product.Store.OwnerId != userId)
                throw new UnauthorizedAccessException("Ban khong co quyen chinh sua san pham nay.");

            if (product.Status == "LOCKED" || product.Status == "SOLD")
                throw new InvalidOperationException($"Khong the chinh sua san pham co trang thai {product.Status}.");

            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId);
            if (!categoryExists)
                throw new KeyNotFoundException("Khong tim thay danh muc san pham.");

            if (request.ImageUrls == null || request.ImageUrls.Count < 1 || request.ImageUrls.Count > 5)
                throw new ArgumentException("San pham can tu 1 den 5 anh.");

            product.CategoryId = request.CategoryId;
            product.Title = request.Title.Trim();
            product.Description = request.Description?.Trim();
            product.Price = request.Price;
            product.Size = request.Size?.Trim();
            product.ConditionPercent = request.ConditionPercent;
            product.StyleTags = request.StyleTags?.Trim();
            product.UpdatedAt = DateTime.UtcNow;

            // Replace images
            _context.ProductImages.RemoveRange(product.Images);
            var newImages = request.ImageUrls
                .Select((url, index) => new ProductImage
                {
                    ProductId = product.Id,
                    ImageUrl = url.Trim(),
                    DisplayOrder = index
                }).ToList();
            _context.ProductImages.AddRange(newImages);

            await _context.SaveChangesAsync();
            return await GetProductDtoAsync(product.Id);
        }

        public async Task DeleteProductAsync(int userId, int productId)
        {
            var product = await _context.Products
                .Include(p => p.Store)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                throw new KeyNotFoundException("Khong tim thay san pham.");

            if (product.Store.OwnerId != userId)
                throw new UnauthorizedAccessException("Ban khong co quyen xoa san pham nay.");

            if (product.Status == "LOCKED" || product.Status == "SOLD")
                throw new InvalidOperationException($"Khong the xoa san pham co trang thai {product.Status}.");

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }

        public async Task<PagedResult<ProductDto>> GetStoreProductsAsync(int storeId, int page, int pageSize)
        {
            var storeExists = await _context.Stores.AnyAsync(s => s.Id == storeId && s.IsActive);
            if (!storeExists)
                throw new KeyNotFoundException("Khong tim thay cua hang hoac cua hang khong hoat dong.");

            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Where(p => p.StoreId == storeId && p.Status == "AVAILABLE")
                .OrderByDescending(p => p.CreatedAt);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ProductDto>
            {
                Items = items.Select(MapToProductDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        private async Task<ProductDto> GetProductDtoAsync(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == productId);

            return MapToProductDto(product!);
        }

        private static ProductDto MapToProductDto(Product p)
        {
            return new ProductDto
            {
                Id = p.Id,
                StoreId = p.StoreId,
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                Size = p.Size,
                ConditionPercent = p.ConditionPercent,
                Status = p.Status,
                StyleTags = p.StyleTags,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                Category = new ProductCategoryDto
                {
                    Id = p.Category.Id,
                    Name = p.Category.Name,
                    Slug = p.Category.Slug
                },
                Images = p.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new ProductImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        DisplayOrder = i.DisplayOrder
                    }).ToList()
            };
        }
    }
}