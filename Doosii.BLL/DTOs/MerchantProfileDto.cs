namespace Doosii.BLL.DTOs
{
    public class MerchantProfileDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string KycStatus { get; set; } = string.Empty;
        public string LicenseImageUrl { get; set; } = string.Empty;
        public string FrontFacadeUrl { get; set; } = string.Empty;
        public string IdCardFrontUrl { get; set; } = string.Empty;
        public string IdCardBackUrl { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
