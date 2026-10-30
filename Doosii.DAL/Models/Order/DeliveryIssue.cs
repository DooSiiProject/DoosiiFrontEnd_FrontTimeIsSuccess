using System.ComponentModel.DataAnnotations;
namespace Doosii.DAL.Models.Order;

public class DeliveryIssue
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    [Required, MaxLength(20)] public string Status { get; set; } = "FAILED";
    [Required, MaxLength(1000)] public string Reason { get; set; } = "";
    [MaxLength(500)] public string? EvidenceUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<DeliveryIssueEvent> Events { get; set; } = new List<DeliveryIssueEvent>();
}
public class DeliveryIssueEvent
{
    public long Id { get; set; }
    public int DeliveryIssueId { get; set; }
    public DeliveryIssue DeliveryIssue { get; set; } = null!;
    public int ActorId { get; set; }
    [Required, MaxLength(30)] public string Action { get; set; } = "";
    [MaxLength(1000)] public string? Note { get; set; }
    [MaxLength(500)] public string? EvidenceUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
