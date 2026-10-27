using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces;
public interface ICommunityInteractionService
{
    Task EnsureAdminAsync(int userId);
    Task<List<CommunityInteractionSummary>> SummariesAsync(string[] keys, int? userId);
    Task<CommunityInteractionSummary> SetLikeAsync(string type, int postId, int userId, bool liked);
    Task<CommunityCommentPage> CommentsAsync(string type, int postId, int page, int pageSize, int? parentId = null);
    Task<CommunityCommentDto> CommentAsync(string type, int postId, int userId, CommunityCommentRequest request);
    Task DeleteCommentAsync(string type, int postId, int commentId, int userId);
    Task<CommunityModerationPost> ModerationPostAsync(int reportId, int userId);
}

