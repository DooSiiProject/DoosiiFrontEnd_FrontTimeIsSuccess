using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Order;

public static class PickupReservationStatus
{
    public const string Pending = "PENDING";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
    public const string Completed = "COMPLETED";
}

/// <summary>Pickup history; service transactions own the product reservation lifecycle.</summary>
[Table("PickupReservations", Schema = "store")]
public class PickupReservation
{
    public int Id { get; set; }
    public int BuyerId { get; set; }
    public int SellerId { get; set; }
    public int StoreId { get; set; }
    public int ProductId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, MaxLength(64)] public string RequestHash { get; set; } = "";
    [Required, MaxLength(20)] public string Status { get; set; } = PickupReservationStatus.Pending;
    [Required, MaxLength(256)] public string ContactName { get; set; } = "";
    [Required, MaxLength(20)] public string ContactPhone { get; set; } = "";
    [MaxLength(500)] public string? BuyerNote { get; set; }
    [Required, MaxLength(300)] public string ProductTitleSnapshot { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal PriceSnapshot { get; set; }
    [Required, MaxLength(8)] public byte[] ProductVersionSnapshot { get; set; } = Array.Empty<byte>();
    [MaxLength(200)] public string? StoreNameSnapshot { get; set; }
    [MaxLength(500)] public string? StoreAddressSnapshot { get; set; }
    public double? StoreLatitudeSnapshot { get; set; }
    public double? StoreLongitudeSnapshot { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ScheduledAt { get; set; }
    public DateTime ResponseDeadline { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? ClosedById { get; set; }
    [MaxLength(500)] public string? CloseReason { get; set; }
    // Only service-side fields; never serialize entities or expose these in a list response.
    [MaxLength(32)] public byte[]? PickupCodeHash { get; set; }
    [MaxLength(2048)] public string? ProtectedPickupCode { get; set; }
    public int FailedCodeAttempts { get; set; }
    public DateTime? CodeLockedUntil { get; set; }
    public DateTime? UpdatedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
