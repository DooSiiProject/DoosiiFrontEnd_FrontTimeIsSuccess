namespace Doosii.BLL.DTOs
{
    public class ProductDetailStoreDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string? StoreAddress { get; set; }
        public string? StorePhone { get; set; }
        public StoreLocationDto? StoreLocation { get; set; }
        public decimal RatingAverage { get; set; }
        public int RatingCount { get; set; }
    }

    public class ProductDetailDto
    {
        public int ProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Size { get; set; }
        public int ConditionPercent { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? StyleTags { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ProductDetailStoreDto Store { get; set; } = null!;
        public ProductCategoryDto Category { get; set; } = null!;
        public List<ProductImageDto> Images { get; set; } = new();
    }
}
