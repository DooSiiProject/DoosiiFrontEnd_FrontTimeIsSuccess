using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Forum;

// One live order owns a PASS item until cancellation or fulfilment.
[Table("PassOrderLocks", Schema = "forum")]
public sealed class PassOrderLock
{
    [Key] public int PostId { get; set; }
    public int OrderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
