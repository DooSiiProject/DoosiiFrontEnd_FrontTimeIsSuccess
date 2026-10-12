using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IReactionService
    {
        Task<ReactionResponseDto> ToggleReactionAsync(int postId, int userId, CreateReactionRequest request);
        Task<PostReactionsSummaryDto> GetReactionsSummaryAsync(int postId, int? currentUserId);
    }
}
