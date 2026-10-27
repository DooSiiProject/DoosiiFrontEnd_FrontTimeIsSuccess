using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Store;

[Table("ShopRelocationRequests", Schema = "store")]
public class ShopRelocationRequest
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public int RequestedByUserId { get; set; }
    public Guid ClientRequestId { get; set; }
    [MaxLength(500)] public string? CurrentAddress { get; set; }
    public double? CurrentLatitude { get; set; }
    public double? CurrentLongitude { get; set; }
    [MaxLength(500)] public string ProposedAddress { get; set; } = string.Empty;
    public double ProposedLatitude { get; set; }
    public double ProposedLongitude { get; set; }
    [MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [MaxLength(20)] public string Status { get; set; } = "PENDING";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    [MaxLength(100)] public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(1000)] public string? ReviewReason { get; set; }
    public Guid? ReviewRequestId { get; set; }
}
