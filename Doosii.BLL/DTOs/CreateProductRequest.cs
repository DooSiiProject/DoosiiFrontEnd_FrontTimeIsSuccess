namespace Doosii.BLL.DTOs;
public class CreateProductRequest : ProductWriteRequest
{
    public Guid? ClientRequestId { get; set; }
}
