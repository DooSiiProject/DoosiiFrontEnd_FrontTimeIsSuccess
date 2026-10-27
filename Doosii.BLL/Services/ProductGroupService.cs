using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Store;
using Microsoft.EntityFrameworkCore;

namespace Doosii.BLL.Services;

public sealed class ProductGroupService(AppDbContext db) : IProductGroupService
{
    private static ServiceException Changed(string code = "GROUP_VERSION_CONFLICT") => new(409, "Dữ liệu đã thay đổi. Tải lại bài và kiểm tra trước khi gửi lại.", code);
    private static void Page(int page, int size)
    {
        if (page < 1 || page > 100000 || size < 1 || size > 24) throw new ArgumentException("Phân trang không hợp lệ.");
    }
    private static void Owner(Store store, int userId, bool write)
    {
        if (store.OwnerId != userId) throw new ServiceException(403, "Bạn không phải chủ shop.", "NOT_OWNER");
        if (write && (!store.IsActive || store.Owner.Role != "Shopper" || store.Owner.MerchantProfile?.KycStatus != "APPROVED"))
            throw new ServiceException(403, "Shop cần đang hoạt động và được duyệt KYC.", "STORE_INELIGIBLE");
    }
    private async Task<Store> LockStore(int storeId, int userId)
    {
        var store = await db.Stores.FromSqlInterpolated($"SELECT * FROM [store].[Stores] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {storeId}").SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy shop.");
        // Same order as escrow: Product -> Store -> seller -> KYC. Recheck current DB role, not JWT role.
        await db.Users.FromSqlInterpolated($"SELECT * FROM [auth].[Users] WITH (HOLDLOCK) WHERE [Id] = {store.OwnerId}").LoadAsync();
        await db.MerchantProfiles.FromSqlInterpolated($"SELECT * FROM [auth].[MerchantProfiles] WITH (HOLDLOCK) WHERE [UserId] = {store.OwnerId}").LoadAsync();
        Owner(store, userId, true);
        return store;
    }
    private IQueryable<ProductGroup> Full() => db.ProductGroups
        .Include(g => g.Store).ThenInclude(s => s.Locations)
        .Include(g => g.Images).Include(g => g.Products).ThenInclude(p => p.Category)
        .Include(g => g.Products).ThenInclude(p => p.Images).AsSplitQuery();
    private IQueryable<ProductGroup> Public() => db.ProductGroups.AsNoTracking().Where(g => g.IsPublished && g.Store.IsActive
        && g.Store.Owner.Role == "Shopper" && g.Store.Owner.MerchantProfile != null && g.Store.Owner.MerchantProfile.KycStatus == "APPROVED");

    private static void Validate(ProductGroupWriteRequest r)
    {
        ProductWriteRules.ValidateObject(r);
        if (r.ClientRequestId == Guid.Empty) throw new ArgumentException("ClientRequestId phải là GUID khác rỗng.");
        r.SharedImageUrls = r.SharedImageUrls.Select(u => u?.Trim() ?? "").ToList();
        if (r.SharedImageUrls.Any(u => !ProductWriteRequest.ImageUrlValid(u)) || r.SharedImageUrls.Distinct(StringComparer.Ordinal).Count() != r.SharedImageUrls.Count)
            throw new ArgumentException("Ảnh chung phải có URL HTTP/HTTPS hợp lệ và không trùng nhau.");
    }
    private static void Keys(IEnumerable<Guid> keys)
    {
        var list = keys.ToList();
        if (list.Any(k => k == Guid.Empty) || list.Distinct().Count() != list.Count) throw new ArgumentException("ClientItemId phải khác rỗng và không trùng nhau.");
    }
    private async Task ValidateNewKeys(int storeId, IEnumerable<Guid> keys)
    {
        var ids = keys.ToArray();
        if (await db.Products.AnyAsync(p => p.StoreId == storeId && p.ClientRequestId.HasValue && ids.Contains(p.ClientRequestId.Value)))
            throw new ServiceException(409, "ClientItemId đã được sử dụng. Không thể gắn sản phẩm có sẵn vào bài.", "IDEMPOTENCY_CONFLICT");
    }
    private async Task<ProductGroupMutation?> Receipt(int storeId, Guid key, string operation, string hash, int? groupId = null)
    {
        var receipt = await db.ProductGroupMutations.AsNoTracking().SingleOrDefaultAsync(m => m.StoreId == storeId && m.ClientRequestId == key);
        if (receipt != null && (receipt.Operation != operation || receipt.PayloadHash != hash || (groupId.HasValue && receipt.ProductGroupId != groupId)))
            throw new ServiceException(409, "ClientRequestId đã dùng cho nội dung hoặc thao tác khác.", "IDEMPOTENCY_CONFLICT");
        return receipt;
    }
    private void SaveReceipt(ProductGroup g, Guid key, string operation, string hash) => db.ProductGroupMutations.Add(new()
        { ProductGroup = g, StoreId = g.StoreId, ClientRequestId = key, Operation = operation, PayloadHash = hash });
    private static List<ProductGroupImage> Images(List<string> urls) => urls.Select((u, i) => new ProductGroupImage { ImageUrl = u, DisplayOrder = i }).ToList();
    private static Product NewProduct(int storeId, Guid key, ProductWriteRequest r, int position)
    {
        var p = new Product { StoreId = storeId, ClientRequestId = key, GroupPosition = position, Status = "AVAILABLE", IsPublished = true, Images = ProductWriteRules.Images(r.ImageUrls) };
        ProductWriteRules.Apply(p, r);
        return p;
    }
    public async Task<ProductGroupWriteResult> CreateAsync(int userId, int storeId, CreateProductGroupRequest request)
    {
        Validate(request); Keys(request.Items.Select(i => i.ClientItemId));
        var products = request.Items.Select(i => ProductWriteRules.Normalize(i.Product)).ToList();
        var hash = Hash(new { operation = "CREATE", storeId, request.Title, request.Description, request.SharedImageUrls,
            items = request.Items.Select((i, index) => new { i.ClientItemId, product = products[index] }) });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockStore(storeId, userId);
        var receipt = await Receipt(storeId, request.ClientRequestId, "CREATE", hash);
        if (receipt != null)
        {
            var current = await LoadManage(receipt.ProductGroupId);
            await tx.CommitAsync(); return new(current, true);
        }
        await ValidateNewKeys(storeId, request.Items.Select(i => i.ClientItemId));
        foreach (var product in products) await ProductWriteRules.ValidateAsync(db, product);
        var group = new ProductGroup { StoreId = storeId, Title = request.Title, Description = request.Description, IsPublished = true, Images = Images(request.SharedImageUrls) };
        for (var i = 0; i < products.Count; i++) group.Products.Add(NewProduct(storeId, request.Items[i].ClientItemId, products[i], i));
        db.ProductGroups.Add(group); SaveReceipt(group, request.ClientRequestId, "CREATE", hash);
        await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(db);
        var result = await LoadManage(group.Id);
        await tx.CommitAsync(); return new(result, false);
    }
    public async Task<ProductGroupWriteResult> UpdateAsync(int userId, int groupId, UpdateProductGroupRequest request)
    {
        Validate(request);
        if (!VersionValid(request.Version) || request.RemovedItems.Any(i => !VersionValid(i.Version)) || request.Items.OfType<UpdateGroupItemRequest>().Any(i => !VersionValid(i.Version)))
            throw new ArgumentException("Version phải là SQL rowversion Base64 hợp lệ.");
        var newItems = request.Items.OfType<NewGroupItemRequest>().ToList(); Keys(newItems.Select(i => i.ClientItemId));
        var hash = UpdateHash(groupId, request);
        // No locks/tracked entities in the pre-read. Do not lock Store before existing Products.
        var initial = await db.ProductGroups.AsNoTracking().Where(g => g.Id == groupId).Select(g => new { g.StoreId, g.Store.OwnerId }).SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy bài gộp.");
        if (initial.OwnerId != userId) throw new ServiceException(403, "Bạn không phải chủ shop.", "NOT_OWNER");
        var ids = await db.Products.AsNoTracking().Where(p => p.ProductGroupId == groupId).Select(p => p.Id).OrderBy(id => id).ToArrayAsync();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        foreach (var id in ids) await db.Products.FromSqlInterpolated($"SELECT * FROM [store].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").AsNoTracking().LoadAsync();
        await LockStore(initial.StoreId, userId);
        await db.ProductGroups.FromSqlInterpolated($"SELECT * FROM [store].[ProductGroups] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {groupId}").LoadAsync();
        var receipt = await Receipt(initial.StoreId, request.ClientRequestId, "UPDATE", hash, groupId);
        if (receipt != null)
        {
            var current = await LoadManage(groupId); await tx.CommitAsync(); return new(current, true);
        }
        var currentIds = await db.Products.AsNoTracking().Where(p => p.ProductGroupId == groupId).Select(p => p.Id).OrderBy(id => id).ToArrayAsync();
        if (!currentIds.SequenceEqual(ids)) throw Changed("MEMBERSHIP_CHANGED");
        // Only load full member rows after the lock set is known to still be complete.
        var group = await Full().SingleAsync(g => g.Id == groupId);
        if (Convert.ToBase64String(group.RowVersion) != request.Version) throw Changed();
        var currentProducts = group.Products.ToDictionary(p => p.Id);
        var retained = request.Items.Select(i => i switch { KeepGroupItemRequest k => (int?)k.ProductId, UpdateGroupItemRequest u => u.ProductId, NewGroupItemRequest => null, _ => throw new ArgumentException("Món không hợp lệ.") }).Where(id => id.HasValue).Select(id => id!.Value);
        var allIds = retained.Concat(request.RemovedItems.Select(i => i.ProductId)).ToArray();
        if (allIds.Length != ids.Length || allIds.Distinct().Count() != allIds.Length || !allIds.OrderBy(id => id).SequenceEqual(ids))
            throw new ArgumentException("Mỗi món hiện tại phải xuất hiện đúng một lần trong items hoặc removedItems.");
        foreach (var item in request.Items.OfType<UpdateGroupItemRequest>()) Editable(currentProducts[item.ProductId], item.Version);
        foreach (var item in request.RemovedItems) Editable(currentProducts[item.ProductId], item.Version);
        await ValidateNewKeys(group.StoreId, newItems.Select(i => i.ClientItemId));
        var writes = new Dictionary<GroupItemRequest, ProductWriteRequest>();
        foreach (var item in request.Items)
        {
            var product = item switch { UpdateGroupItemRequest u => u.Product, NewGroupItemRequest n => n.Product, _ => null };
            if (product == null) continue;
            var normalized = ProductWriteRules.Normalize(product); await ProductWriteRules.ValidateAsync(db, normalized); writes[item] = normalized;
        }
        // Everything is validated before mutating tracked rows. One save/transaction includes the receipt.
        foreach (var item in request.RemovedItems)
        {
            var p = currentProducts[item.ProductId]; p.ProductGroup = null; p.ProductGroupId = null; p.GroupPosition = null; p.UpdatedAt = DateTime.UtcNow;
        }
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            if (item is NewGroupItemRequest added) { group.Products.Add(NewProduct(group.StoreId, added.ClientItemId, writes[item], i)); continue; }
            var id = item is KeepGroupItemRequest keep ? keep.ProductId : ((UpdateGroupItemRequest)item).ProductId;
            var p = currentProducts[id]; p.GroupPosition = i;
            if (item is UpdateGroupItemRequest)
            {
                ProductWriteRules.Apply(p, writes[item]); p.UpdatedAt = DateTime.UtcNow;
                db.ProductImages.RemoveRange(p.Images); p.Images = ProductWriteRules.Images(writes[item].ImageUrls);
            }
        }
        group.Title = request.Title; group.Description = request.Description; group.IsPublished = request.IsPublished; group.UpdatedAt = DateTime.UtcNow;
        // Force a version bump even on a same-value/item-only request.
        db.Entry(group).Property(g => g.UpdatedAt).IsModified = true;
        db.ProductGroupImages.RemoveRange(group.Images); group.Images = Images(request.SharedImageUrls);
        SaveReceipt(group, request.ClientRequestId, "UPDATE", hash);
        await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(db); var result = await LoadManage(group.Id);
        await tx.CommitAsync(); return new(result, false);
    }
    private static void Editable(Product p, string version)
    {
        if (p.Status != "AVAILABLE" || p.ReservedOrderId != null) throw new ServiceException(409, "Món đang giữ hoặc đã bán chỉ có thể KEEP.", "PRODUCT_NOT_EDITABLE");
        if (Convert.ToBase64String(p.RowVersion) != version) throw Changed("PRODUCT_VERSION_CONFLICT");
    }
    private static bool VersionValid(string? version) => version != null && Convert.TryFromBase64String(version, new Span<byte>(new byte[8]), out var bytes) && bytes == 8;
    private async Task<ProductGroupManageDto> LoadManage(int id) => Manage(await Full().AsNoTracking().SingleAsync(g => g.Id == id));
    public async Task<ProductGroupManageDto> ManageAsync(int userId, int groupId)
    {
        var group = await Full().AsNoTracking().SingleOrDefaultAsync(g => g.Id == groupId) ?? throw new KeyNotFoundException("Không tìm thấy bài gộp.");
        Owner(group.Store, userId, false); return Manage(group);
    }
    public async Task<PagedResult<ProductGroupManageDto>> MineAsync(int userId, int page, int pageSize)
    {
        Page(page, pageSize);
        var query = Full().AsNoTracking().Where(g => g.Store.OwnerId == userId);
        var count = await query.CountAsync();
        var groups = await query.OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new() { Items = groups.Select(Manage).ToList(), TotalCount = count, Page = page, PageSize = pageSize };
    }
    public async Task<PagedResult<ProductGroupSummaryDto>> StoreAsync(int storeId, int page, int pageSize)
    {
        Page(page, pageSize);
        if (!await db.Stores.AnyAsync(s => s.Id == storeId && s.IsActive && s.Owner.Role == "Shopper" && s.Owner.MerchantProfile != null && s.Owner.MerchantProfile.KycStatus == "APPROVED"))
            throw new KeyNotFoundException("Không tìm thấy shop đang hoạt động.");
        var query = Public().Where(g => g.StoreId == storeId && g.Products.Any(p => p.IsPublished && p.Status == "AVAILABLE"));
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(g => new ProductGroupSummaryDto { Id = g.Id, StoreId = g.StoreId, Title = g.Title, CreatedAt = g.CreatedAt, UpdatedAt = g.UpdatedAt,
                CoverImageUrl = g.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                AvailableCount = g.Products.Count(p => p.IsPublished && p.Status == "AVAILABLE"), VisibleProductCount = g.Products.Count(p => p.IsPublished),
                MinAvailablePrice = g.Products.Where(p => p.IsPublished && p.Status == "AVAILABLE").Min(p => (decimal?)p.Price) }).ToListAsync();
        foreach (var item in items)
        {
            item.CreatedAt = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc);
            item.UpdatedAt = item.UpdatedAt.HasValue ? DateTime.SpecifyKind(item.UpdatedAt.Value, DateTimeKind.Utc) : null;
        }
        return new() { Items = items, TotalCount = count, Page = page, PageSize = pageSize };
    }
    public async Task<ProductGroupDetailDto> DetailAsync(int groupId)
    {
        if (!await Public().AnyAsync(g => g.Id == groupId && g.Products.Any(p => p.IsPublished))) throw new KeyNotFoundException("Không tìm thấy bài gộp.");
        // Recheck publication/store eligibility on the actual read as well.
        var group = await Full().AsNoTracking().Where(g => g.IsPublished && g.Store.IsActive && g.Store.Owner.Role == "Shopper"
            && g.Store.Owner.MerchantProfile != null && g.Store.Owner.MerchantProfile.KycStatus == "APPROVED" && g.Products.Any(p => p.IsPublished))
            .SingleOrDefaultAsync(g => g.Id == groupId) ?? throw new KeyNotFoundException("Không tìm thấy bài gộp.");
        var dto = new ProductGroupDetailDto { Description = group.Description,
            SharedImages = group.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => new ProductImageDto { Id = i.Id, ImageUrl = i.ImageUrl, DisplayOrder = i.DisplayOrder }).ToList(),
            Products = group.Products.Where(p => p.IsPublished).OrderBy(p => p.GroupPosition).ThenBy(p => p.Id).Select(p => ProductWriteRules.Map(p, false)).ToList(),
            Store = StoreSummary(group.Store) };
        Summary(group, dto); return dto;
    }
    private static ProductDetailStoreDto StoreSummary(Store s)
    {
        var loc = s.Locations.OrderBy(l => l.Id).FirstOrDefault();
        return new() { StoreId = s.Id, StoreName = s.Name, StoreAddress = s.Address ?? loc?.Address, StorePhone = s.Phone,
            RatingAverage = s.RatingAverage, RatingCount = s.RatingCount,
            StoreLocation = loc == null ? null : new StoreLocationDto { Id = loc.Id, Latitude = loc.Latitude, Longitude = loc.Longitude, Address = loc.Address } };
    }
    private static List<string> OrderedImages(ProductGroup g) => g.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).ToList();
    private static void Summary(ProductGroup g, ProductGroupSummaryDto dto)
    {
        var visible = g.Products.Where(p => p.IsPublished).ToList(); var available = visible.Where(p => p.Status == "AVAILABLE").ToList();
        dto.Id = g.Id; dto.StoreId = g.StoreId; dto.Title = g.Title;
        dto.CreatedAt = DateTime.SpecifyKind(g.CreatedAt, DateTimeKind.Utc);
        dto.UpdatedAt = g.UpdatedAt.HasValue ? DateTime.SpecifyKind(g.UpdatedAt.Value, DateTimeKind.Utc) : null;
        dto.CoverImageUrl = OrderedImages(g).FirstOrDefault(); dto.VisibleProductCount = visible.Count; dto.AvailableCount = available.Count; dto.MinAvailablePrice = available.Select(p => (decimal?)p.Price).Min();
    }
    private static ProductGroupManageDto Manage(ProductGroup g)
    {
        var dto = new ProductGroupManageDto { Description = g.Description, IsPublished = g.IsPublished, Version = Convert.ToBase64String(g.RowVersion), SharedImageUrls = OrderedImages(g),
            Products = g.Products.OrderBy(p => p.GroupPosition).ThenBy(p => p.Id).Select(p => new ProductGroupManagedItemDto {
                ProductId = p.Id, ClientItemId = p.ClientRequestId, GroupPosition = p.GroupPosition!.Value, Version = Convert.ToBase64String(p.RowVersion),
                Status = p.Status, IsPublished = p.IsPublished, CanEdit = p.Status == "AVAILABLE" && p.ReservedOrderId == null, CanDetach = p.Status == "AVAILABLE" && p.ReservedOrderId == null,
                Product = ProductWriteRules.Map(p) }).ToList() };
        Summary(g, dto); return dto;
    }
    private static string UpdateHash(int groupId, UpdateProductGroupRequest r) => Hash(new { operation = "UPDATE", groupId, r.Version, r.Title, r.Description, r.IsPublished, r.SharedImageUrls,
        items = r.Items.Select(i => i switch {
            KeepGroupItemRequest k => (object)new { operation = "KEEP", k.ProductId },
            UpdateGroupItemRequest u => new { operation = "UPDATE", u.ProductId, u.Version, product = ProductWriteRules.Normalize(u.Product) },
            NewGroupItemRequest n => new { operation = "NEW", n.ClientItemId, product = ProductWriteRules.Normalize(n.Product) },
            _ => throw new ArgumentException("Món không hợp lệ.") }), r.RemovedItems });
    // Fixed/sorted property names, decimal scale normalization; meaningful array order remains intact.
    private static string Hash(object payload)
    {
        using var document = JsonSerializer.SerializeToDocument(payload);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) { Canonical(writer, document.RootElement); }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void Canonical(Utf8JsonWriter w, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject(); foreach (var p in e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { w.WritePropertyName(p.Name); Canonical(w, p.Value); } w.WriteEndObject(); break;
            case JsonValueKind.Array: w.WriteStartArray(); foreach (var item in e.EnumerateArray()) Canonical(w, item); w.WriteEndArray(); break;
            case JsonValueKind.Number when e.TryGetDecimal(out var d): w.WriteRawValue(d.ToString("G29", CultureInfo.InvariantCulture)); break;
            default: e.WriteTo(w); break;
        }
    }
}
