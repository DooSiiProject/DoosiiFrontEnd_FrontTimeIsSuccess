using System.ComponentModel.DataAnnotations;
namespace Doosii.BLL.DTOs;
public class UpdateProductRequest : ProductWriteRequest
{
    [Required] public string Version { get; set; } = "";
}
