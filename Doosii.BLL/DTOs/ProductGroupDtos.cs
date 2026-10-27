using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Doosii.BLL.DTOs;

// Strict parsing applies only to the new group contract, including product metadata.
[JsonConverter(typeof(GroupProductWriteConverter))]
public sealed class GroupProductWriteRequest : ProductWriteRequest;

public sealed class GroupProductWriteConverter : JsonConverter<GroupProductWriteRequest>
{
    public override GroupProductWriteRequest Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var strict = new JsonSerializerOptions(options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var p = JsonSerializer.Deserialize<ProductWriteRequest>(ref reader, strict) ?? throw new JsonException("Thiếu sản phẩm.");
        return new() { CategoryId = p.CategoryId, Title = p.Title?.Trim() ?? "", Description = p.Description?.Trim(), Price = p.Price,
            Size = p.Size?.Trim(), ConditionPercent = p.ConditionPercent, StyleTags = p.StyleTags?.Trim(), ImageUrls = p.ImageUrls, Details = p.Details };
    }
    public override void Write(Utf8JsonWriter writer, GroupProductWriteRequest value, JsonSerializerOptions options) => JsonSerializer.Serialize<ProductWriteRequest>(writer, value, options);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ProductGroupWriteRequest
{
    public Guid ClientRequestId { get; set; }
    private string title = "", description = "";
    [Required, MaxLength(120)] public string Title { get => title; set => title = value?.Trim() ?? ""; }
    [Required, MaxLength(2000)] public string Description { get => description; set => description = value?.Trim() ?? ""; }
    [Required, MinLength(1), MaxLength(30)] public List<string> SharedImageUrls { get; set; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateProductGroupRequest : ProductGroupWriteRequest
{
    [Required, MinLength(2), MaxLength(30)] public List<NewGroupProductRequest> Items { get; set; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class NewGroupProductRequest
{
    public Guid ClientItemId { get; set; }
    [Required] public GroupProductWriteRequest Product { get; set; } = null!;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateProductGroupRequest : ProductGroupWriteRequest
{
    [Required] public string Version { get; set; } = "";
    [JsonRequired] public bool IsPublished { get; set; }
    [Required, MinLength(2), MaxLength(30)] public List<GroupItemRequest> Items { get; set; } = [];
    [Required, MaxLength(30)] public List<RemovedGroupProductRequest> RemovedItems { get; set; } = [];
}
[JsonConverter(typeof(GroupItemConverter))]
public abstract class GroupItemRequest;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class KeepGroupItemRequest : GroupItemRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateGroupItemRequest : GroupItemRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Required] public string Version { get; set; } = "";
    [Required] public GroupProductWriteRequest Product { get; set; } = null!;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class NewGroupItemRequest : GroupItemRequest
{
    public Guid ClientItemId { get; set; }
    [Required] public GroupProductWriteRequest Product { get; set; } = null!;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RemovedGroupProductRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Required] public string Version { get; set; } = "";
}
// Discriminator may occur anywhere. Each variant rejects fields belonging to another variant.
public sealed class GroupItemConverter : JsonConverter<GroupItemRequest>
{
    public override bool CanConvert(Type type) => type == typeof(GroupItemRequest);
    public override GroupItemRequest Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader) as JsonObject ?? throw new JsonException("Món không hợp lệ.");
        if (!node.Remove("operation", out var operation) || operation is not JsonValue v || !v.TryGetValue<string>(out var kind)) throw new JsonException("Thiếu operation.");
        return kind switch {
            "KEEP" => node.Deserialize<KeepGroupItemRequest>(options)!,
            "UPDATE" => node.Deserialize<UpdateGroupItemRequest>(options)!,
            "NEW" => node.Deserialize<NewGroupItemRequest>(options)!,
            _ => throw new JsonException("operation phải là KEEP, UPDATE hoặc NEW.")
        };
    }
    public override void Write(Utf8JsonWriter writer, GroupItemRequest value, JsonSerializerOptions options)
    {
        var node = JsonSerializer.SerializeToNode(value, value.GetType(), options)!.AsObject();
        node["operation"] = value switch { KeepGroupItemRequest => "KEEP", UpdateGroupItemRequest => "UPDATE", NewGroupItemRequest => "NEW", _ => throw new JsonException() };
        node.WriteTo(writer, options);
    }
}

public class ProductGroupSummaryDto
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public string Title { get; set; } = "";
    public string? CoverImageUrl { get; set; }
    public int AvailableCount { get; set; }
    public int VisibleProductCount { get; set; }
    public decimal? MinAvailablePrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
public sealed class ProductGroupDetailDto : ProductGroupSummaryDto
{
    public string Description { get; set; } = "";
    public List<ProductImageDto> SharedImages { get; set; } = [];
    public ProductDetailStoreDto Store { get; set; } = null!;
    public List<ProductDto> Products { get; set; } = [];
}
public sealed class ProductGroupManageDto : ProductGroupSummaryDto
{
    public string Description { get; set; } = "";
    public bool IsPublished { get; set; }
    public string Version { get; set; } = "";
    public List<string> SharedImageUrls { get; set; } = [];
    public List<ProductGroupManagedItemDto> Products { get; set; } = [];
}
public sealed class ProductGroupManagedItemDto
{
    public int ProductId { get; set; }
    public Guid? ClientItemId { get; set; }
    public int GroupPosition { get; set; }
    public string Version { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsPublished { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDetach { get; set; }
    public ProductDto Product { get; set; } = null!;
}
public sealed record ProductGroupWriteResult(ProductGroupManageDto Group, bool Replayed);
