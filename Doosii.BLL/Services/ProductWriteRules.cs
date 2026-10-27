using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Doosii.BLL.DTOs;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Store;
using Microsoft.EntityFrameworkCore;

namespace Doosii.BLL.Services;

internal static class ProductWriteRules
{
    // Service callers receive the same recursive validation as HTTP callers.
    public static void ValidateObject(object value)
    {
        var errors = new List<ValidationResult>();
        ValidateTree(value, errors);
        if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors.Select(e => e.ErrorMessage).Distinct()));
    }
    private static void ValidateTree(object value, List<ValidationResult> errors)
    {
        Validator.TryValidateObject(value, new ValidationContext(value), errors, true);
        foreach (var property in value.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            var child = property.GetValue(value);
            if (child == null || child is string || child.GetType().IsValueType) continue;
            if (child is IEnumerable list) { foreach (var item in list) if (item == null) errors.Add(new("Danh sách không được có phần tử null.")); else if (item is not string && !item.GetType().IsValueType) ValidateTree(item, errors); }
            else ValidateTree(child, errors);
        }
    }
    public static async Task ValidateAsync(AppDbContext db, ProductWriteRequest request, bool categoryAsBadRequest = true)
    {
        ValidateObject(request);
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId))
        {
            if (categoryAsBadRequest) throw new ArgumentException("Không tìm thấy danh mục sản phẩm.");
            throw new KeyNotFoundException("Không tìm thấy danh mục sản phẩm.");
        }
    }
    public static ProductWriteRequest Normalize(ProductWriteRequest request)
    {
        var node = JsonSerializer.SerializeToNode<ProductWriteRequest>(request)!;
        TrimStrings(node);
        return node.Deserialize<ProductWriteRequest>()!;
    }
    private static void TrimStrings(JsonNode node)
    {
        if (node is JsonObject obj) foreach (var key in obj.Select(p => p.Key).ToArray())
        {
            if (obj[key] is JsonValue val && val.TryGetValue<string>(out var s)) obj[key] = s.Trim();
            else if (obj[key] is JsonNode child) TrimStrings(child);
        }
        else if (node is JsonArray array) for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is JsonValue val && val.TryGetValue<string>(out var s)) array[i] = s.Trim();
            else if (array[i] is JsonNode child) TrimStrings(child);
        }
    }
    public static void Apply(Product p, ProductWriteRequest r)
    {
        p.CategoryId = r.CategoryId; p.Title = r.Title.Trim(); p.Description = r.Description?.Trim(); p.Price = r.Price;
        p.Size = r.Size?.Trim(); p.ConditionPercent = r.ConditionPercent; p.StyleTags = r.StyleTags?.Trim();
        p.DetailsJson = r.Details == null ? null : JsonSerializer.Serialize(r.Details);
    }
    public static List<ProductImage> Images(List<string> urls) => urls.Select((url, i) => new ProductImage { ImageUrl = url.Trim(), DisplayOrder = i }).ToList();
    public static ProductListingDetails? ReadDetails(Product p) => p.DetailsJson == null ? null : JsonSerializer.Deserialize<ProductListingDetails>(p.DetailsJson);
    public static ProductDto Map(Product p, bool owner = true) => new()
    {
        Id = p.Id, StoreId = p.StoreId, ProductGroupId = owner || p.ProductGroup?.IsPublished == true ? p.ProductGroupId : null,
        Details = ReadDetails(p), IsPublished = p.IsPublished, Version = Convert.ToBase64String(p.RowVersion),
        Title = p.Title, Description = p.Description, Price = p.Price, Size = p.Size, ConditionPercent = p.ConditionPercent,
        Status = p.Status, StyleTags = p.StyleTags, CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt,
        Category = new() { Id = p.Category.Id, Name = p.Category.Name, Slug = p.Category.Slug },
        Images = p.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => new ProductImageDto { Id = i.Id, ImageUrl = i.ImageUrl, DisplayOrder = i.DisplayOrder }).ToList()
    };
}
