using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Doosii.DAL.Models.Store;

[Table("ProductGroups", Schema = "store")]
public class ProductGroup
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    [Required, MaxLength(120)] public string Title { get; set; } = "";
    [Required, MaxLength(2000)] public string Description { get; set; } = "";
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public Store Store { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<ProductGroupImage> Images { get; set; } = new List<ProductGroupImage>();
}

[Table("ProductGroupImages", Schema = "store")]
public class ProductGroupImage
{
    public int Id { get; set; }
    public int ProductGroupId { get; set; }
    [Required, MaxLength(1000)] public string ImageUrl { get; set; } = "";
    public int DisplayOrder { get; set; }
    public ProductGroup ProductGroup { get; set; } = null!;
}

[Table("ProductGroupMutations", Schema = "store")]
public class ProductGroupMutation
{
    public long Id { get; set; }
    public int StoreId { get; set; }
    public int ProductGroupId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, MaxLength(10)] public string Operation { get; set; } = "";
    [Required, Column(TypeName = "char(64)")] public string PayloadHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ProductGroup ProductGroup { get; set; } = null!;
}
