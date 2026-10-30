using System.ComponentModel.DataAnnotations;

namespace Doosii.DAL.Models.Order;

// A return is separate from the existing escrow dispute and never proves a bank transfer by itself.
public class ReturnRequest
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int BuyerId { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [MaxLength(4000)] public string? EvidenceJson { get; set; }
    [Required, MaxLength(32)] public string Status { get; set; } = "REQUESTED";
    [MaxLength(1000)] public string? SellerNote { get; set; }
    [MaxLength(100)] public string? ReturnCarrier { get; set; }
    [MaxLength(100)] public string? ReturnTrackingCode { get; set; }
    [MaxLength(500)] public string? RefundEvidenceUrl { get; set; }
    [MaxLength(1000)] public string? AdminNote { get; set; }
    [MaxLength(20)] public string? ShippingFeePayer { get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? RefundConfirmedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<ReturnRequestEvent> Events { get; set; } = new List<ReturnRequestEvent>();
}

public class ReturnRequestEvent
{
    public long Id { get; set; }
    public int ReturnRequestId { get; set; }
    public ReturnRequest ReturnRequest { get; set; } = null!;
    public int ActorId { get; set; }
    [Required, MaxLength(40)] public string Action { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
