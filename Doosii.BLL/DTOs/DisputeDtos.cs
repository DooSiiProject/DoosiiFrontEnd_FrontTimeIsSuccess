using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class CreateDisputeRequest
    {
        [Required(ErrorMessage = "Lý do khiếu nại là bắt buộc.")]
        [MaxLength(1000, ErrorMessage = "Lý do khiếu nại tối đa 1000 ký tự.")]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Đường dẫn video mở hàng tối đa 500 ký tự.")]
        public string? EvidenceVideoUrl { get; set; }

        public List<string>? EvidenceImages { get; set; }
    }

    public class DisputeResponse
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int RaisedByUserId { get; set; }
        public string RaisedByUserName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? EvidenceVideoUrl { get; set; }
        public List<string> EvidenceImages { get; set; } = new List<string>();
        public string Status { get; set; } = string.Empty;
        public string? AdminVerdict { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
