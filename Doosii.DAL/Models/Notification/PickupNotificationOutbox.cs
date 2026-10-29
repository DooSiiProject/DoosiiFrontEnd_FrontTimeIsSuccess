using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Notification;

[Table("PickupNotificationOutbox", Schema = "notification")]
public class PickupNotificationOutbox
{
    public long Id { get; set; }
    public int NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;
    [Required, MaxLength(160)] public string EventKey { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime AvailableAt { get; set; }
    public int Attempts { get; set; }
}
