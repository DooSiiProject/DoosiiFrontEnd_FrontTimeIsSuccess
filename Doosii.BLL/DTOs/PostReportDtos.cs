using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class CreatePostReportRequest
    {
        [Required(ErrorMessage = "PostType is required")]
        public string PostType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Reason is required")]
        [MaxLength(30, ErrorMessage = "Reason cannot exceed 30 characters")]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        public List<string>? EvidenceUrls { get; set; }
    }

    public class ResolvePostReportRequest
    {
        [Required(ErrorMessage = "Action is required")]
        public string Action { get; set; } = string.Empty;

        [MaxLength(2000, ErrorMessage = "AdminNote cannot exceed 2000 characters")]
        public string? AdminNote { get; set; }
    }

    public class PostReportQuery
    {
        public string? Status { get; set; } = "PENDING";
        public string? Reason { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class PostReportResponseDto
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string PostType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string> EvidenceUrls { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? AdminNote { get; set; }
        public PostSummaryDto Post { get; set; } = null!;
        public UserSummaryDto Reporter { get; set; } = null!;
        public UserSummaryDto? ReviewedBy { get; set; }
    }

    public class PostSummaryDto
    {
        public int Id { get; set; }
        public string PostType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class UserSummaryDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }
}
