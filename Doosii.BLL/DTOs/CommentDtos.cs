using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class CreateCommentRequest
    {
        [Required(ErrorMessage = "Content la bat buoc.")]
        [MaxLength(1000, ErrorMessage = "Content toi da 1000 ky tu.")]
        public string Content { get; set; } = string.Empty;

        public int? ParentCommentId { get; set; }
    }

    public class CommentResponseDto
    {
        public int CommentId { get; set; }
        public int PostId { get; set; }
        public int AuthorId { get; set; }
        public string AuthorDisplayName { get; set; } = string.Empty;
        public string? AuthorAvatarUrl { get; set; }
        public string Content { get; set; } = string.Empty;
        public int? ParentCommentId { get; set; }
        public List<CommentResponseDto> Replies { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
