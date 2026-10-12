using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;

namespace Doosii.BLL.Services
{
    public class ReactionService : IReactionService
    {
        private static readonly HashSet<string> ValidReactionTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "LIKE",
            "HEART"
        };

        private readonly AppDbContext _db;
        private readonly ILogger<ReactionService> _logger;

        public ReactionService(AppDbContext db, ILogger<ReactionService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Tha cam xuc hoac huy/cap nhat cam xuc (Toggle reaction):
        /// - Chua co reaction: tao moi (action = ADDED).
        /// - Da co va gui CUNG loai: xoa reaction (action = REMOVED).
        /// - Da co va gui KHAC loai: cap nhat sang loai moi (action = UPDATED).
        /// </summary>
        public async Task<ReactionResponseDto> ToggleReactionAsync(int postId, int userId, CreateReactionRequest request)
        {
            // 1. Validate reaction type
            if (string.IsNullOrWhiteSpace(request?.ReactionType) || !ValidReactionTypes.Contains(request.ReactionType.Trim()))
            {
                throw new ServiceException(400,
                    "Loai reaction khong hop le. Chi chap nhan 'LIKE' hoac 'HEART'.",
                    "REACTION_TYPE_INVALID");
            }

            var type = request.ReactionType.Trim().ToUpperInvariant();

            // 2. Validate user
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ServiceException(401, "Nguoi dung khong hop le.", "UNAUTHORIZED");
            }

            if (user.Role != null && user.Role.Equals("Banned", StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceException(403, "Tai khoan cua ban da bi khoa.", "USER_BANNED");
            }

            // 3. Validate post exists and is active
            var postActive = await CheckPostActiveAsync(postId);
            if (!postActive)
            {
                throw new ServiceException(404, "Khong tim thay bai dang hoac bai dang da bi an.", "POST_NOT_FOUND");
            }

            // 4. Toggle logic
            var existing = await _db.Reactions
                .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

            string action;
            string? currentReaction;

            if (existing == null)
            {
                var reaction = new Reaction
                {
                    PostId = postId,
                    UserId = userId,
                    ReactionType = type,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Reactions.Add(reaction);
                action = "ADDED";
                currentReaction = type;
            }
            else if (existing.ReactionType.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                _db.Reactions.Remove(existing);
                action = "REMOVED";
                currentReaction = null;
            }
            else
            {
                existing.ReactionType = type;
                action = "UPDATED";
                currentReaction = type;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("Reaction toggled on Post {PostId} by User {UserId}: Action={Action}, Type={Type}",
                postId, userId, action, currentReaction);

            // 5. Lay tong so reaction tren post
            var (likeCount, heartCount) = await GetCountsAsync(postId);

            return new ReactionResponseDto
            {
                Action = action,
                ReactionType = currentReaction,
                LikeCount = likeCount,
                HeartCount = heartCount,
                CurrentUserReaction = currentReaction
            };
        }

        /// <summary>
        /// Lay thong tin tong so LIKE va HEART tren bai viet, kem reaction cua current user neu da dang nhap.
        /// </summary>
        public async Task<PostReactionsSummaryDto> GetReactionsSummaryAsync(int postId, int? currentUserId)
        {
            var postActive = await CheckPostActiveAsync(postId);
            if (!postActive)
            {
                throw new ServiceException(404, "Khong tim thay bai dang hoac bai dang da bi an.", "POST_NOT_FOUND");
            }

            var (likeCount, heartCount) = await GetCountsAsync(postId);

            string? userReaction = null;
            if (currentUserId.HasValue)
            {
                userReaction = await _db.Reactions
                    .AsNoTracking()
                    .Where(r => r.PostId == postId && r.UserId == currentUserId.Value)
                    .Select(r => r.ReactionType)
                    .FirstOrDefaultAsync();
            }

            return new PostReactionsSummaryDto
            {
                LikeCount = likeCount,
                HeartCount = heartCount,
                CurrentUserReaction = userReaction
            };
        }

        // -------- Helpers --------

        private async Task<(int LikeCount, int HeartCount)> GetCountsAsync(int postId)
        {
            var grouped = await _db.Reactions
                .AsNoTracking()
                .Where(r => r.PostId == postId)
                .GroupBy(r => r.ReactionType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            var likeCount = grouped.FirstOrDefault(g => g.Type.Equals("LIKE", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
            var heartCount = grouped.FirstOrDefault(g => g.Type.Equals("HEART", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

            return (likeCount, heartCount);
        }

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
    }
}
