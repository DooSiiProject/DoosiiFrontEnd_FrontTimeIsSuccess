using System.Data;
using static Doosii.BLL.Services.ProductWriteRules;

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
        public ProductService(AppDbContext context) { _context = context; }

        private IQueryable<Product> ManagedQuery() => _context.Products.Include(p => p.Category).Include(p => p.Images).Include(p => p.Store).ThenInclude(s => s.Owner).ThenInclude(o => o.MerchantProfile);
        private static void EnsureOwner(Store store, int userId, bool write = true)
        {
            if (store.OwnerId != userId) throw new UnauthorizedAccessException("Bạn không có quyền quản lý sản phẩm này.");
            if (write && (!store.IsActive || store.Owner.Role != "Shopper" || store.Owner.MerchantProfile?.KycStatus != "APPROVED")) throw new UnauthorizedAccessException("Shop cần đang hoạt động và được duyệt KYC.");
        }
        private static void EnsureEditable(Product p, string? version = null)
        {
            if (p.Status != "AVAILABLE" || p.ReservedOrderId != null || p.ReservedPickupReservationId != null) throw new InvalidOperationException("Không thể sửa sản phẩm đang được giữ hoặc đã bán.");
            if (version != null && Convert.ToBase64String(p.RowVersion) != version) throw new InvalidOperationException("Sản phẩm đã thay đổi. Tải lại dữ liệu trước khi lưu.");
        }
        private Task ValidateRequest(ProductWriteRequest request) => ValidateAsync(_context, request, categoryAsBadRequest: false);

        public async Task<ProductDto> CreateProductAsync(int userId, int storeId, CreateProductRequest request)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            // Serialize retries on the store, including requests that race before the first insert.
            var store = await _context.Stores.FromSqlInterpolated($"SELECT * FROM [store].[Stores] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {storeId}").Include(s => s.Owner).ThenInclude(o => o.MerchantProfile).SingleOrDefaultAsync();
            if (store == null) throw new KeyNotFoundException("Không tìm thấy shop.");
            EnsureOwner(store, userId);
            if (request.ClientRequestId.HasValue)
            {
                var existing = await _context.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Images).FirstOrDefaultAsync(p => p.StoreId == storeId && p.ClientRequestId == request.ClientRequestId);
                if (existing != null) { await tx.CommitAsync(); return MapToProductDto(existing); }
            }
            await ValidateRequest(request);
            var product = new Product { StoreId = storeId, ClientRequestId = request.ClientRequestId, Images = Images(request.ImageUrls) };
            Apply(product, request);
            _context.Products.Add(product);
            await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); // Product and all images are committed together.
            await tx.CommitAsync();
            return await GetProductDtoAsync(product.Id);
        }
        private async Task<Product> GetForWrite(int productId)
        {
            // Order writes to this row wait until edits/publication changes are committed.
            await _context.Products.FromSqlInterpolated($"SELECT * FROM [store].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {productId}").LoadAsync();
            return await ManagedQuery().SingleOrDefaultAsync(p => p.Id == productId) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
        }
        public async Task<ProductDto> UpdateProductAsync(int userId, int productId, UpdateProductRequest request)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var product = await GetForWrite(productId);
            EnsureOwner(product.Store, userId); EnsureEditable(product, request.Version);
            await ValidateRequest(request);
            Apply(product, request); product.UpdatedAt = DateTime.UtcNow;
            _context.ProductImages.RemoveRange(product.Images); product.Images = Images(request.ImageUrls);
            await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); await tx.CommitAsync();
            return await GetProductDtoAsync(product.Id);
        }
        public async Task<ProductDto> SetPublicationAsync(int userId, int productId, ProductPublicationRequest request)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var product = await GetForWrite(productId);
            EnsureOwner(product.Store, userId); EnsureEditable(product, request.Version);
            product.IsPublished = request.IsPublished; product.UpdatedAt = DateTime.UtcNow;
            await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); await tx.CommitAsync();
            return await GetProductDtoAsync(product.Id);
        }
        public async Task DeleteProductAsync(int userId, int productId)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var product = await GetForWrite(productId);
            EnsureOwner(product.Store, userId); EnsureEditable(product);
            if (product.ProductGroupId != null) throw new InvalidOperationException("Hãy gỡ món khỏi bài gộp trước khi xóa sản phẩm.");
            if (await _context.PostMedia.AnyAsync(m => m.ProductId == productId))
                throw new InvalidOperationException("Sản phẩm đang được nhắc trong bài cộng đồng. Hãy ẩn sản phẩm thay vì xóa để giữ liên kết và lịch sử.");
            if (await _context.PickupReservations.AnyAsync(r => r.ProductId == productId))
                throw new InvalidOperationException("Sản phẩm đã có lịch sử giữ tại shop. Hãy ẩn thay vì xóa.");
            _context.Products.Remove(product); await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); await tx.CommitAsync();
        }
        public async Task<ProductDto> GetManagedProductAsync(int userId, int productId)
        {
            var product = await ManagedQuery().AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            EnsureOwner(product.Store, userId, false);
            return MapToProductDto(product);
        }
        public async Task<PagedResult<ProductDto>> GetMyProductsAsync(int userId, int page, int pageSize, string? state)
        {
            var query = _context.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Images).Where(p => p.Store.OwnerId == userId);
            query = state switch {
                "AVAILABLE" => query.Where(p => p.Status == "AVAILABLE" && p.IsPublished),
                "HIDDEN" => query.Where(p => p.Status == "AVAILABLE" && !p.IsPublished),
                "LOCKED" or "SOLD" => query.Where(p => p.Status == state),
                _ => query
            };
            var count = await query.CountAsync();
            var items = await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PagedResult<ProductDto> { Items = items.Select(MapToProductDto).ToList(), TotalCount = count, Page = page, PageSize = pageSize };
        }
        public async Task<PagedResult<ProductCatalogItemDto>> GetCatalogAsync(ProductCatalogQuery filter, CancellationToken cancellationToken = default)
        {
            var errors = filter.Validate();
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
            var query = _context.Products.AsNoTracking().Where(p => p.Status == "AVAILABLE" && p.IsPublished
                && p.Store.IsActive && p.Store.Owner.Role == "Shopper"
                && p.Store.Owner.MerchantProfile != null && p.Store.Owner.MerchantProfile.KycStatus == "APPROVED");
            var term = filter.Q?.Trim();
            // Title search follows the existing SQL Server collation;
            // Contains parameterizes and escapes wildcard characters as literal text.
            if (!string.IsNullOrEmpty(term)) query = query.Where(p => p.Title.Contains(term));
            if (filter.CategoryId.HasValue) query = query.Where(p => p.CategoryId == filter.CategoryId);
            if (filter.MinPrice.HasValue) query = query.Where(p => p.Price >= filter.MinPrice);
            if (filter.MaxPrice.HasValue) query = query.Where(p => p.Price <= filter.MaxPrice);
            var count = await query.CountAsync(cancellationToken);
            var ordered = filter.Sort switch
            {
                "price-asc" => query.OrderBy(p => p.Price).ThenByDescending(p => p.Id),
                "price-desc" => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.Id),
                _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            };
            var items = await ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
                .Select(p => new ProductCatalogItemDto
                {
                    Id = p.Id, ProductGroupId = p.ProductGroup != null && p.ProductGroup.IsPublished ? p.ProductGroupId : null, SellerId = p.Store.OwnerId, Title = p.Title, Price = p.Price, Size = p.Size,
                    ConditionPercent = p.ConditionPercent, Status = p.Status, IsPublished = p.IsPublished, CreatedAt = p.CreatedAt,
                    Store = new ProductCatalogStoreDto { StoreId = p.StoreId, StoreName = p.Store.Name },
                    Category = new ProductCategoryDto { Id = p.Category.Id, Name = p.Category.Name, Slug = p.Category.Slug },
                    Images = p.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => new ProductImageDto
                        { Id = i.Id, ImageUrl = i.ImageUrl, DisplayOrder = i.DisplayOrder }).ToList()
                }).ToListAsync(cancellationToken);
            return new PagedResult<ProductCatalogItemDto> { Items = items, TotalCount = count, Page = filter.Page, PageSize = filter.PageSize };
        }
        public async Task<PagedResult<ProductDto>> GetStoreProductsAsync(int storeId, int page, int pageSize, int? categoryId = null)
        {
            var storeExists = await _context.Stores.AnyAsync(s => s.Id == storeId && s.IsActive && s.Owner.Role == "Shopper" && s.Owner.MerchantProfile != null && s.Owner.MerchantProfile.KycStatus == "APPROVED");
            if (!storeExists)
                throw new KeyNotFoundException("Khong tim thay cua hang hoac cua hang khong hoat dong.");

            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Images).Include(p => p.ProductGroup)
                .Where(p => p.StoreId == storeId && p.Status == "AVAILABLE" && p.IsPublished && (!categoryId.HasValue || p.CategoryId == categoryId.Value))
                .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ProductDto>
            {
                Items = items.Select(p => Map(p, false)).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Xem chi tiet mot san pham (public). Tra 404 neu product khong ton tai hoac store inactive.
        /// Product AVAILABLE / LOCKED / SOLD deu xem duoc nhung tra dung status.
        /// </summary>
        public async Task<ProductDetailDto> GetProductDetailAsync(int productId)
        {
            // Single query - lay product kem Store (va Location), Category, Images, khong lay thong tin nhay cam cua Owner
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Store)
                    .ThenInclude(s => s.Locations)
                .Include(p => p.Category)
                .Include(p => p.Images).Include(p => p.ProductGroup)
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsPublished && p.Store.Owner.Role == "Shopper" && p.Store.Owner.MerchantProfile != null && p.Store.Owner.MerchantProfile.KycStatus == "APPROVED");

            if (product == null)
                throw new KeyNotFoundException("PRODUCT_NOT_FOUND");

            // Kiem tra store phai ton tai va dang active
            if (!product.Store.IsActive)
                throw new KeyNotFoundException("STORE_NOT_ACTIVE");

            var primaryLocation = product.Store.Locations.FirstOrDefault();

            return new ProductDetailDto
            {
                ProductId = product.Id, ProductGroupId = product.ProductGroup?.IsPublished == true ? product.ProductGroupId : null, SellerId = product.Store.OwnerId,
                Details = ReadDetails(product), IsPublished = product.IsPublished, Version = Convert.ToBase64String(product.RowVersion),
                Title = product.Title,
                Description = product.Description,
                Price = product.Price,
                Size = product.Size,
                ConditionPercent = product.ConditionPercent,
                Status = product.Status,
                StyleTags = product.StyleTags,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                Store = new ProductDetailStoreDto
                {
                    StoreId = product.Store.Id,
                    StoreName = product.Store.Name,
                    StoreAddress = product.Store.Address ?? primaryLocation?.Address,
                    StorePhone = product.Store.Phone,
                    StoreLocation = primaryLocation != null ? new StoreLocationDto
                    {
                        Id = primaryLocation.Id,
                        Latitude = primaryLocation.Latitude,
                        Longitude = primaryLocation.Longitude,
                        Address = primaryLocation.Address
                    } : null,
                    RatingAverage = product.Store.RatingAverage,
                    RatingCount = product.Store.RatingCount
                },
                Category = new ProductCategoryDto
                {
                    Id = product.Category.Id,
                    Name = product.Category.Name,
                    Slug = product.Category.Slug
                },
                Images = product.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new ProductImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        DisplayOrder = i.DisplayOrder
                    }).ToList()
            };
        }

        public async Task<List<ProductCategoryDto>> GetCategoriesAsync()
        {
            return await _context.Categories.AsNoTracking()
                .OrderBy(c => c.Name).ThenBy(c => c.Id)
                .Select(c => new ProductCategoryDto { Id = c.Id, Name = c.Name, Slug = c.Slug })
                .ToListAsync();
        }

        // ============ Private helpers ============

        private async Task<ProductDto> GetProductDtoAsync(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == productId);

            return MapToProductDto(product!);
        }

        private static ProductDto MapToProductDto(Product p) => Map(p);
    }
}
