using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum;

[Table("Posts", Schema = "forum")]
public class Post
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    [Required, MaxLength(10)] public string Type { get; set; } = "OTHER";
    [Required, MaxLength(150)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(5000)] public string PostDescription { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string VisibilityStatus { get; set; } = "ACTIVE";
    public Guid ClientRequestId { get; set; }
    [MaxLength(64)] public string? RequestHash { get; set; }
    public bool HiddenByAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public User Author { get; set; } = null!;
    public PassPostInfo? PassInfo { get; set; }
    public ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();
}
