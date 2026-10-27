using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum;

[Table("PassPostInfos", Schema = "forum")]
public class PassPostInfo
{
    [Key] public int PostId { get; set; }
    // Integrity discriminator: the composite FK only permits a PASS parent.
    [Required, MaxLength(10)] public string PostType { get; set; } = "PASS";
    [Required, MaxLength(300)] public string ProductName { get; set; } = string.Empty;
    [MaxLength(2000)] public string? ProductDescription { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
    [Required, MaxLength(50)] public string Size { get; set; } = string.Empty;
    public int ConditionPercent { get; set; } = 100;
    [Required, MaxLength(2000)] public string Defects { get; set; } = string.Empty;
    [Required, MaxLength(4000)] public string StyleTagsJson { get; set; } = "[]";
    public string? MeasurementsJson { get; set; }
    [Required, MaxLength(20)] public string SaleStatus { get; set; } = "AVAILABLE";
    public Post Post { get; set; } = null!;
}
