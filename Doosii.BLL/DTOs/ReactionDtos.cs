using System.ComponentModel.DataAnnotations;

namespace Doosii.BLL.DTOs
{
    public class CreateReactionRequest
    {
        [Required(ErrorMessage = "ReactionType la bat buoc.")]
        public string ReactionType { get; set; } = "LIKE";
    }

    public class ReactionResponseDto
    {
        public string Action { get; set; } = string.Empty; // ADDED | UPDATED | REMOVED
        public string? ReactionType { get; set; }
        public int LikeCount { get; set; }
        public int HeartCount { get; set; }
        public string? CurrentUserReaction { get; set; }
    }

    public class PostReactionsSummaryDto
    {
        public int LikeCount { get; set; }
        public int HeartCount { get; set; }
        public string? CurrentUserReaction { get; set; }
    }
}
