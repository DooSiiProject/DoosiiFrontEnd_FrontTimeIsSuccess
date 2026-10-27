using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models;

[Table("UploadedImages", Schema = "media")]
public class UploadedImage
{
    public long Id { get; set; }
    public int OwnerId { get; set; }
    [Required, MaxLength(500)] public string PublicId { get; set; } = "";
    [Required, MaxLength(1000)] public string Url { get; set; } = "";
    [Required, MaxLength(16)] public string State { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
