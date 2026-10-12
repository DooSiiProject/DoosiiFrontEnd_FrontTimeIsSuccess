using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;

namespace Doosii.BLL.Services
{
    public class CommentService : ICommentService
    {
        private const int MaxContentLength = 1000;
        private readonly AppDbContext _db;
        private readonly ILogger<CommentService> _logger;

        public CommentService(AppDbContext db, ILogger<CommentService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Dang binh luan moi (ho tro comment goc hoac reply cap 1).
        /// </summary>
        public async Task<CommentResponseDto> CreateCommentAsync(int postId, int authorId, CreateCommentRequest request)
        {
            // 1. Validate content
            if (string.IsNullOrWhiteSpace(request?.Content) || request.Content.Trim().Length > MaxContentLength)
            {
                throw new ServiceException(400,
                    $"Noi dung binh luan la bat buoc va khong duoc vuot qua {MaxContentLength} ky tu.",
                    "COMMENT_CONTENT_INVALID");
            }

            // 2. Validate author
            var author = await _db.Users.FindAsync(authorId);
            if (author == null)
            {
                throw new ServiceException(401, "Nguoi dung khong hop le.", "UNAUTHORIZED");
            }

            if (author.Role != null && author.Role.Equals("Banned", StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceException(403, "Tai khoan cua ban da bi khoa.", "USER_BANNED");
            }

            // 3. Validate post exists and is public (PassPost hoac SpotSharingPost)
            var postActive = await CheckPostActiveAsync(postId);
            if (!postActive)
            {
                throw new ServiceException(404, "Khong tim thay bai dang hoac bai dang da bi an.", "POST_NOT_FOUND");
            }

            // 4. Validate hierarchy neu la reply
            if (request.ParentCommentId.HasValue)
            {
                var parent = await _db.Comments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.ParentCommentId.Value);

                if (parent == null)
                {
                    throw new ServiceException(404, "Khong tim thay binh luan cha.", "COMMENT_NOT_FOUND");
                }

                if (parent.PostId != postId)
                {
                    throw new ServiceException(400, "Binh luan cha khong thuoc ve bai viet nay.", "PARENT_COMMENT_MISMATCH");
                }

                // Chi cho phep toi da 2 cap: comment goc (cap 1) -> reply (cap 2).
                // Neu parent da co ParentCommentId -> day la comment cap 2 -> khong cho reply nua!
                if (parent.ParentCommentId != null)
                {
                    throw new ServiceException(400, "Replies can only be one level deep", "COMMENT_DEPTH_EXCEEDED");
                }
            }

            // 5. Luu binh luan moi
            var comment = new Comment
            {
                PostId = postId,
                AuthorId = authorId,
                ParentCommentId = request.ParentCommentId,
                Content = request.Content.Trim(),
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Comment {CommentId} created on Post {PostId} by User {AuthorId}",
                comment.Id, postId, authorId);

            return new CommentResponseDto
            {
                CommentId = comment.Id,
                PostId = comment.PostId,
                AuthorId = author.Id,
                AuthorDisplayName = !string.IsNullOrWhiteSpace(author.FullName) ? author.FullName : $"User #{author.Id}",
                AuthorAvatarUrl = author.AvatarUrl,
                Content = comment.Content,
                ParentCommentId = comment.ParentCommentId,
                Replies = new List<CommentResponseDto>(),
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                IsDeleted = comment.IsDeleted
            };
        }

        /// <summary>
        /// Lay danh sach binh luan theo cay phan cap 2 cap: Root comments -> Replies.
        /// </summary>
        public async Task<List<CommentResponseDto>> GetCommentsByPostIdAsync(int postId)
        {
            var postActive = await CheckPostActiveAsync(postId);
            if (!postActive)
            {
                throw new ServiceException(404, "Khong tim thay bai dang hoac bai dang da bi an.", "POST_NOT_FOUND");
            }

            var allComments = await _db.Comments
                .AsNoTracking()
                .Include(c => c.Author)
                .Where(c => c.PostId == postId)
                .ToListAsync();

            // Gom nhom replies theo ParentCommentId va sap xep theo CreatedAt ASC
            var repliesGroup = allComments
                .Where(c => c.ParentCommentId != null)
                .GroupBy(c => c.ParentCommentId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(r => r.CreatedAt).Select(MapToDto).ToList()
                );

            // Lay danh sach comment goc, sap xep theo CreatedAt DESC (moi nhat truoc)
            var rootComments = allComments
                .Where(c => c.ParentCommentId == null)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c =>
                {
                    var dto = MapToDto(c);
                    if (repliesGroup.TryGetValue(c.Id, out var replies))
                    {
                        dto.Replies = replies;
                    }
                    return dto;
                })
                .ToList();

            return rootComments;
        }

        /// <summary>
        /// Xoa mem binh luan (IsDeleted = true).
        /// Chi Author hoac Admin duoc phep xoa.
        /// </summary>
        public async Task DeleteCommentAsync(int commentId, int currentUserId)
        {
            var comment = await _db.Comments.FindAsync(commentId);
            if (comment == null)
            {
                throw new ServiceException(404, "Khong tim thay binh luan.", "COMMENT_NOT_FOUND");
            }

            var user = await _db.Users.FindAsync(currentUserId);
            if (user == null)
            {
                throw new ServiceException(401, "Nguoi dung khong hop le.", "UNAUTHORIZED");
            }

            if (user.Role != null && user.Role.Equals("Banned", StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceException(403, "Tai khoan cua ban da bi khoa.", "USER_BANNED");
            }

            bool isAdmin = user.Role != null && user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            if (comment.AuthorId != currentUserId && !isAdmin)
            {
                throw new ServiceException(403, "Ban khong co quyen xoa binh luan nay.", "FORBIDDEN");
            }

            comment.IsDeleted = true;
            comment.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Comment {CommentId} soft-deleted by User {UserId}", commentId, currentUserId);
        }

        // -------- Helpers --------

        private async Task<bool> CheckPostActiveAsync(int postId)
        {
            var passPost = await _db.PassPosts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (passPost != null)
            {
                return passPost.Status == "ACTIVE";
            }

            var spotPost = await _db.SpotSharingPosts
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == postId);

            if (spotPost != null)
            {
                return spotPost.Status == "ACTIVE";
            }

            return false;
        }

        private static CommentResponseDto MapToDto(Comment c)
        {
            var content = c.IsDeleted ? "Bình luận này đã bị xóa." : c.Content;
            var authorName = c.Author != null && !string.IsNullOrWhiteSpace(c.Author.FullName)
                ? c.Author.FullName
                : $"User #{c.AuthorId}";

            return new CommentResponseDto
            {
                CommentId = c.Id,
                PostId = c.PostId,
                AuthorId = c.AuthorId,
                AuthorDisplayName = authorName,
                AuthorAvatarUrl = c.Author?.AvatarUrl,
                Content = content,
                ParentCommentId = c.ParentCommentId,
                Replies = new List<CommentResponseDto>(),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                IsDeleted = c.IsDeleted
            };
        }
    }
}
