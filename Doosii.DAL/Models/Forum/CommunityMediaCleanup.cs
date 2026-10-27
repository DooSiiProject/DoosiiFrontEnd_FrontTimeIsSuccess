using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum;

// Registered BEFORE cloud upload; removed atomically when attached to a post.
// Unfinished uploads and detached media are retried by the cleanup worker.
[Table("CommunityMediaCleanup", Schema = "forum")]
public class CommunityMediaCleanup
{
    [Key, MaxLength(500)] public string PublicId { get; set; } = string.Empty;
    [Required, MaxLength(10)] public string MediaType { get; set; } = "IMAGE";
    public DateTime DeleteAfter { get; set; }
    public int Attempts { get; set; }
}
