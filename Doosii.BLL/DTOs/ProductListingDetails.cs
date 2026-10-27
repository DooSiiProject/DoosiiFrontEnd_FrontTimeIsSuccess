using System.ComponentModel.DataAnnotations;
namespace Doosii.BLL.DTOs;

public class ProductListingDetails
{
    [MaxLength(100)] public string? Brand { get; set; }
    [MaxLength(100)] public string? Origin { get; set; }
    [Required, MaxLength(100)] public string Material { get; set; } = "";
    [Required, MaxLength(30)] public string GarmentType { get; set; } = "";
    [Required, MaxLength(20)] public string Condition { get; set; } = "";
    public List<string> Colors { get; set; } = new();
    public List<string> StyleTagIds { get; set; } = new();
    public bool HasDefects { get; set; }
    public List<ProductMeasurement> Measurements { get; set; } = new();
    public string? SizeGuideUrl { get; set; }
    public List<ProductDefect> Defects { get; set; } = new();
}
public class ProductMeasurement
{
    [Required, MaxLength(80)] public string Name { get; set; } = "";
    [Required, MaxLength(20)] public string Unit { get; set; } = "";
    [Range(0.000001, 1000000)] public double Value { get; set; }
}
public class ProductDefect
{
    [Required, MaxLength(80)] public string Type { get; set; } = "";
    [Required, MaxLength(80)] public string Location { get; set; } = "";
    [Required, MaxLength(20)] public string Severity { get; set; } = "";
    [MaxLength(200)] public string? Note { get; set; }
    public List<string> ImageUrls { get; set; } = new();
}
public class ProductWriteRequest : IValidatableObject
{
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [Required, MaxLength(300)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    [Range(0.01, 9999999999999999.0)] public decimal Price { get; set; }
    [MaxLength(50)] public string? Size { get; set; }
    [Range(0, 100)] public int ConditionPercent { get; set; } = 100;
    [MaxLength(500)] public string? StyleTags { get; set; }
    [Required, MinLength(1), MaxLength(5)] public List<string> ImageUrls { get; set; } = new();
    public ProductListingDetails? Details { get; set; }
    public static bool ImageUrlValid(string? url) => url?.Length <= 1000 && Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http");
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ImageUrls == null || ImageUrls.Any(u => !ImageUrlValid(u))) yield return new("URL ảnh không hợp lệ.");
        if (Details == null) yield break; // Keep older products readable without inventing metadata.
        if (Details.Colors == null || Details.Colors.Count < 1 || Details.Colors.Count > 20 || Details.Colors.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 50)) yield return new("Cần từ 1 đến 20 màu, tối đa 50 ký tự mỗi màu.");
        if (Details.StyleTagIds == null || Details.StyleTagIds.Count > 50 || Details.StyleTagIds.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 64)) yield return new("Style-tag không hợp lệ.");
        if (!new[] { "new", "excellent", "good", "fair" }.Contains(Details.Condition)) yield return new("Tình trạng không hợp lệ.");
        if (!new[] { "top", "bottom", "outerwear", "footwear", "accessory", "full-body" }.Contains(Details.GarmentType)) yield return new("Loại trang phục không hợp lệ.");
        if (Details.SizeGuideUrl != null && !ImageUrlValid(Details.SizeGuideUrl)) yield return new("URL bảng size không hợp lệ.");
        if (Details.Measurements == null || Details.Measurements.Count > 30 || Details.Measurements.Any(m => m == null || !double.IsFinite(m.Value) || m.Value <= 0 || m.Value > 1000000)) yield return new("Số đo không hợp lệ, tối đa 30 dòng.");
        if (Details.Defects == null || Details.Defects.Count > 30 || Details.HasDefects != (Details.Defects.Count > 0)) yield return new("Danh sách khuyết điểm không khớp lựa chọn, tối đa 30 mục.");
        if (Details.Defects != null && Details.Defects.Any(d => d == null || d.ImageUrls == null || d.ImageUrls.Count > 5 || d.ImageUrls.Any(u => !ImageUrlValid(u)))) yield return new("Mỗi khuyết điểm tối đa 5 ảnh có URL hợp lệ.");
    }
}
public class ProductPublicationRequest
{
    public bool IsPublished { get; set; }
    [Required] public string Version { get; set; } = "";
}
