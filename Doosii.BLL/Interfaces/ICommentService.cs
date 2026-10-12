using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface ICommentService
    {
        Task<CommentResponseDto> CreateCommentAsync(int postId, int authorId, CreateCommentRequest request);
        Task<List<CommentResponseDto>> GetCommentsByPostIdAsync(int postId);
        Task DeleteCommentAsync(int commentId, int currentUserId);
    }
}
