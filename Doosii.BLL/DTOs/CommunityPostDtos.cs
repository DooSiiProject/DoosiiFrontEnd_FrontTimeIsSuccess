using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Doosii.BLL.DTOs;

public class CommunityPostForm
{
    [Required] public string Payload { get; set; } = "";
    public List<IFormFile> Files { get; set; } = new();
}
public class CommunityPostWriteRequest
{
    public Guid ClientRequestId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string PostDescription { get; set; } = "";
    public string? Version { get; set; }
    public CommunityPassInfo? PassInfo { get; set; }
    public List<CommunityMediaWrite> Media { get; set; } = new();
}
public class CommunityPassInfo
{
    public string ProductName { get; set; } = "";
    public string? ProductDescription { get; set; }
    public decimal Price { get; set; }
    public string Size { get; set; } = "";
    public int ConditionPercent { get; set; }
    public string Defects { get; set; } = "";
    public List<string> StyleTagIds { get; set; } = new();
    public List<ProductMeasurement>? Measurements { get; set; }
}
public class CommunityMediaWrite
{
    public int? Id { get; set; }
    public string? UploadKey { get; set; }
    public string MediaType { get; set; } = "";
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }
    public int? StoreId { get; set; }
    public int? ProductId { get; set; }
}
public class CommunityPostQuery
{
    public string Tab { get; set; } = "newest";
    public string? Style { get; set; }
    public int? StoreId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double RadiusKm { get; set; } = 5;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
public class CommunityPage<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
public class CommunityPostDto
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public UserSummaryDto Author { get; set; } = new();
    public string Title { get; set; } = "";
    public string PostDescription { get; set; } = "";
    public string VisibilityStatus { get; set; } = "";
    public bool HiddenByAdmin { get; set; }
    public CommunityPassInfo? PassInfo { get; set; }
    public string? SaleStatus { get; set; }
    public List<CommunityMediaDto> Media { get; set; } = new();
    public CommunityInteractionSummary Summary { get; set; } = new();
    public string Version { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public double? DistanceKm { get; set; }
}
public class CommunityMediaDto
{
    public int Id { get; set; }
    public string MediaType { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string? ThumbnailUrl { get; set; }
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }
    public CommunityStoreAttachment? Store { get; set; }
    public CommunityProductAttachment? Product { get; set; }
    public bool AttachmentUnavailable { get; set; }
}
public record CommunityStoreAttachment(int Id, string Name, double? Latitude, double? Longitude);
public record CommunityProductAttachment(int Id, string Title, decimal Price, string Status);
public class CommunityVisibilityRequest
{
    public string VisibilityStatus { get; set; } = "";
    public string Version { get; set; } = "";
}
public class CommunitySaleRequest
{
    public string SaleStatus { get; set; } = "";
    public string Version { get; set; } = "";
}
public record CommunityUploadedMedia(string PublicId, string MediaType, string SourceUrl, string? ThumbnailUrl);
