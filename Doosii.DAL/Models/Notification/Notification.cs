using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Notification
{
    public static class NotificationType
    {
        public const string OrderUpdate = "ORDER_UPDATE";
        public const string WalletAlert = "WALLET_ALERT";
        public const string BaleOpening = "BALE_OPENING";
        public const string DisputeUpdate = "DISPUTE_UPDATE";
        public const string System = "SYSTEM";
    }

    [Table("Notifications", Schema = "notification")]
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = NotificationType.System;

        [MaxLength(500)]
        public string? TargetUrl { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
