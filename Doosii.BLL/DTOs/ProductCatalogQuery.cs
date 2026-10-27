namespace Doosii.BLL.DTOs;

public class ProductCatalogQuery
{
    public string? Q { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Page < 1 || Page > 1000000 || PageSize < 1 || PageSize > 24) errors.Add("Phân trang không hợp lệ.");
        if (CategoryId is <= 0) errors.Add("Danh mục không hợp lệ.");
        if (Q?.Length > 100) errors.Add("Từ khóa tối đa 100 ký tự.");
        if (MinPrice is < 0 or > 1000000000000m || MaxPrice is < 0 or > 1000000000000m || MinPrice > MaxPrice) errors.Add("Khoảng giá không hợp lệ.");
        if (!new[] { "newest", "price-asc", "price-desc" }.Contains(Sort)) errors.Add("Cách sắp xếp không hợp lệ.");
        return errors;
    }
}

// Public catalog cards intentionally contain only product and shop display data.
public class ProductCatalogItemDto
{
        public int? ProductGroupId { get; set; }
    public int Id { get; set; }
    public int SellerId { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
    public string? Size { get; set; }
    public int ConditionPercent { get; set; }
    public string Status { get; set; } = "";
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public ProductCategoryDto Category { get; set; } = null!;
    public ProductCatalogStoreDto Store { get; set; } = null!;
    public List<ProductImageDto> Images { get; set; } = new();
}
public class ProductCatalogStoreDto
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = "";
}

