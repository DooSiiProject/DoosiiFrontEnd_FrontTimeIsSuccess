using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Doosii.BLL.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateShopRelocationRequest
{
    [Required, MaxLength(500)] public string ProposedAddress { get; set; } = string.Empty;
    [Required, Range(-90d, 90d)] public double? ProposedLatitude { get; set; }
    [Required, Range(-180d, 180d)] public double? ProposedLongitude { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public Guid? ClientRequestId { get; set; }
}

public class ShopRelocationResponse
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public string? CurrentAddress { get; set; }
    public double? CurrentLatitude { get; set; }
    public double? CurrentLongitude { get; set; }
    public string ProposedAddress { get; set; } = string.Empty;
    public double ProposedLatitude { get; set; }
    public double ProposedLongitude { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewReason { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ReviewShopRelocationRequest
{
    [Required] public string Decision { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public Guid? ClientRequestId { get; set; }
}

public class AdminShopRelocationResponse
{
    public ShopRelocationResponse Request { get; set; } = new();
    public string StoreName { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? LiveAddress { get; set; }
    public double? LiveLatitude { get; set; }
    public double? LiveLongitude { get; set; }
    public bool CanApprove { get; set; }
    public string? ApprovalBlockReason { get; set; }
}

public class AdminShopRelocationPage
{
    public List<AdminShopRelocationResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ShopRelocationPage
{
    public List<ShopRelocationResponse> Items { get; set; } = new();
    public ShopRelocationResponse? PendingRequest { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
