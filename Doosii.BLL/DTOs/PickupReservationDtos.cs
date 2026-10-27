using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs;

// Target contract for B3/B4. No controller/service is registered in B1/B2.
public class CreatePickupReservationRequest : IValidatableObject
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required] public string ProductVersion { get; set; } = "";
    public DateTimeOffset ScheduledAt { get; set; }
    [Required, MaxLength(256)] public string ContactName { get; set; } = "";
    [Required, Phone, MaxLength(20)] public string ContactPhone { get; set; } = "";
    [MaxLength(500)] public string? BuyerNote { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ClientRequestId == Guid.Empty) yield return new("Mã yêu cầu không hợp lệ.", new[] { nameof(ClientRequestId) });
        if (!PickupContractValidation.IsVersion(ProductVersion)) yield return new("Version sản phẩm không hợp lệ.", new[] { nameof(ProductVersion) });
        if (ScheduledAt == default) yield return new("Cần chọn thời gian đến shop.", new[] { nameof(ScheduledAt) });
        // Relative time, quota, role and shop eligibility must use server time/DB in B3.
    }
}
public class PickupVersionRequest : IValidatableObject
{
    [Required] public string Version { get; set; } = "";
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!PickupContractValidation.IsVersion(Version)) yield return new("Version lượt giữ không hợp lệ.", new[] { nameof(Version) });
    }
}
public class ClosePickupReservationRequest : PickupVersionRequest
{
    [Required, MaxLength(500)] public string Reason { get; set; } = "";
}
public class CompletePickupReservationRequest : PickupVersionRequest
{
    [Required, RegularExpression("^[0-9]{6}$")] public string PickupCode { get; set; } = "";
    public bool ConfirmedPaymentReceived { get; set; }
    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in base.Validate(validationContext)) yield return error;
        if (!ConfirmedPaymentReceived) yield return new("Shop cần xác nhận đã giao hàng và nhận đủ tiền.", new[] { nameof(ConfirmedPaymentReceived) });
    }
}
public class PickupReservationQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 24)] public int PageSize { get; set; } = 12;
    [RegularExpression("^(PENDING|ACCEPTED|REJECTED|CANCELLED|EXPIRED|COMPLETED)$")] public string? Status { get; set; }
}
public class PickupReservationPage
{
    public List<PickupReservationDto> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public DateTimeOffset ServerTime { get; set; }
}
public class PickupReservationDto
{
    public int Id { get; set; }
    public int BuyerId { get; set; }
    public int SellerId { get; set; }
    public int StoreId { get; set; }
    public int ProductId { get; set; }
    public string Status { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string? BuyerNote { get; set; }
    public string ProductTitle { get; set; } = "";
    public decimal Price { get; set; }
    public string? StoreName { get; set; }
    public string? StoreAddress { get; set; }
    public double? StoreLatitude { get; set; }
    public double? StoreLongitude { get; set; }
    public bool StoreLocationChanged { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset ResponseDeadline { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string Version { get; set; } = "";
    public DateTimeOffset ServerTime { get; set; }
}
// Buyer-only, private no-store response. Never embed in list, notification or seller DTO.
public class PickupCodeDto
{
    public int ReservationId { get; set; }
    public string PickupCode { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset ServerTime { get; set; }
}
internal static class PickupContractValidation
{
    internal static bool IsVersion(string? value)
    {
        try { return value != null && Convert.FromBase64String(value).Length == 8; }
        catch (FormatException) { return false; }
    }
}
