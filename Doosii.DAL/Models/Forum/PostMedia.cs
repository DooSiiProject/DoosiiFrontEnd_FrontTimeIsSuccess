using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Doosii.DAL.Models.Store;

namespace Doosii.DAL.Models.Forum;

[Table("PostMedia", Schema = "forum")]
public class PostMedia
{
    public int Id { get; set; }
    public int PostId { get; set; }
    // Internal discriminator, always copied from Post.Type, never trusted from a client.
    [Required, MaxLength(10)] public string PostType { get; set; } = "OTHER";
    [Required, MaxLength(10)] public string MediaType { get; set; } = "IMAGE";
    [Required, MaxLength(1000)] public string SourceUrl { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string StoragePublicId { get; set; } = string.Empty;
    [MaxLength(1000)] public string? ThumbnailUrl { get; set; }
    [MaxLength(1000)] public string? Caption { get; set; }
    public int DisplayOrder { get; set; }
    public int? StoreId { get; set; }
    public int? ProductId { get; set; }
    public Post Post { get; set; } = null!;
    public Store.Store? Store { get; set; }
    public Product? Product { get; set; }
}
