using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs;

public class CommunityInteractionSummary
{
    public int PostId { get; set; }
    public string PostType { get; set; } = "";
    public int ReactionCount { get; set; }
    public int CommentCount { get; set; }
    public bool LikedByMe { get; set; }
}
public class CommunityCommentRequest
{
    [Required, MaxLength(1000)] public string Content { get; set; } = "";
    public int? ParentCommentId { get; set; }
    public Guid ClientRequestId { get; set; }
}
public class CommunityCommentDto
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string PostType { get; set; } = "";
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string Content { get; set; } = "";
    public int? ParentCommentId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class CommunityCommentPage
{
    public List<CommunityCommentDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
public class CommunityModerationPost
{
    public List<CommunityMediaDto> Media { get; set; } = new();
    public int ReportId { get; set; }
    public int PostId { get; set; }
    public string PostType { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Status { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public decimal? Price { get; set; }
    public string? LocationName { get; set; }
    public List<FeedImageDto> Images { get; set; } = new();
}

public class CommunityReportRequest
{
    public List<string>? EvidenceUrls { get; set; }
    [Required, MaxLength(30)] public string Reason { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
}
