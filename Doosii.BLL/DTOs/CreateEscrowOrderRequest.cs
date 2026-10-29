using System.ComponentModel.DataAnnotations;
namespace Doosii.BLL.DTOs;
public class CreateEscrowOrderRequest : IValidatableObject
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, RegularExpression("^(PAYOS|COD)$")] public string PaymentMethod { get; set; } = "PAYOS";
    [Required] public string ProductVersion { get; set; } = "";
    [Required, MaxLength(256)] public string ReceiverName { get; set; } = "";
    [Required, Phone, MaxLength(20)] public string ReceiverPhone { get; set; } = "";
    [Required, MaxLength(500)] public string ShippingAddress { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ClientRequestId == Guid.Empty) yield return new("Mã yêu cầu không hợp lệ.");
    }
}
public class OrderQuoteResponse
{
    public int ProductId { get; set; }
    public int? PassPostId { get; set; }
    public int SellerId { get; set; }
    public string ProductName { get; set; } = "";
    public string? Thumbnail { get; set; }
    public string ProductVersion { get; set; } = "";
    public decimal ProductPrice { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal EscrowFee { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal EscrowFeeRate { get; set; }
}

public sealed class CreatePassCodRequest : IValidatableObject
{
    [Range(1, int.MaxValue)] public int PassPostId { get; set; }
    [Required] public string ProductVersion { get; set; } = "";
    public Guid ClientRequestId { get; set; }
    [Required, MaxLength(256)] public string ReceiverName { get; set; } = "";
    [Required, Phone, MaxLength(20)] public string ReceiverPhone { get; set; } = "";
    [Required, MaxLength(500)] public string ShippingAddress { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ClientRequestId == Guid.Empty) yield return new("Mã yêu cầu không hợp lệ.");
        if (string.IsNullOrWhiteSpace(ReceiverName) || string.IsNullOrWhiteSpace(ReceiverPhone) || string.IsNullOrWhiteSpace(ShippingAddress)) yield return new("Thông tin nhận hàng không hợp lệ.");
    }
}
