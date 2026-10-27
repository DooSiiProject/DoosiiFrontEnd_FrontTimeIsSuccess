using System.Data;
using Doosii.BLL.Helpers;
using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Forum;

namespace Doosii.BLL.Services;

public class CommunityInteractionService(AppDbContext db) : ICommunityInteractionService
{
    private static string Type(string value) => value?.ToUpperInvariant() is "PASS" or "SPOT" ? value.ToUpperInvariant() :
        throw new ServiceException(400, "Loai bai phai la PASS hoac SPOT.", "INVALID_POST_TYPE");

    private async Task<User> UserAsync(int userId, bool admin = false)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ServiceException(401, "Tai khoan khong ton tai.", "UNAUTHORIZED");
        if (user.Role == "Banned" || admin && user.Role != "Admin")
            throw new ServiceException(403, "Tai khoan khong co quyen thuc hien thao tac.", "FORBIDDEN");
        return user;
    }
    private async Task PublicPostAsync(string type, int id)
    {
        var exists = type == "PASS"
            ? await db.PassPosts.AnyAsync(p => p.Id == id && (p.Status == "ACTIVE" || p.Status == "LOCKED" || p.Status == "SOLD") && p.Author.Role != "Banned")
            : await db.SpotSharingPosts.AnyAsync(p => p.Id == id && p.Status == "ACTIVE" && p.Author.Role != "Banned");
        if (!exists) throw new ServiceException(404, "Bai viet khong ton tai hoac khong con cong khai.", "POST_NOT_FOUND");
    }
    private async Task<T> WriteAsync<T>(string type, int postId, Func<Task<T>> action)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            // Serialize writes to one typed post before acquiring row/range locks.
            var resource = $"community:{type}:{postId}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000;
                IF @lockResult < 0 THROW 51001, 'Community write conflict', 1;
                """);
            var result = await action();
            await tx.CommitAsync();
            return result;
        }
        catch (Exception ex) when (CommunityDatabaseErrors.IsConflict(ex))
        {
            throw new ServiceException(409, "Du lieu vua thay doi. Vui long thu lai.", "COMMUNITY_CONFLICT");
        }
    }
    public async Task EnsureAdminAsync(int userId) { await UserAsync(userId, true); }
    public async Task<List<CommunityInteractionSummary>> SummariesAsync(string[] keys, int? userId)
    {
        if (keys.Length > 50) throw new ServiceException(400, "Toi da 50 bai moi yeu cau.", "INVALID_POST_KEYS");
        if (userId.HasValue) await UserAsync(userId.Value);
        var result = new List<CommunityInteractionSummary>();
        var parsed = new List<(string Type, int Id)>();
        foreach (var key in keys.Distinct())
        {
            var parts = key.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var id) || id < 1)
                throw new ServiceException(400, "Dinh danh bai khong hop le.", "INVALID_POST_KEYS");
            parsed.Add((Type(parts[0]), id));
        }
        foreach (var type in new[] { "PASS", "SPOT" })
        {
            var ids = parsed.Where(p => p.Type == type).Select(p => p.Id).Distinct().ToList();
            if (ids.Count == 0) continue;
            var visible = type == "PASS"
                ? await db.PassPosts.Where(p => ids.Contains(p.Id) && (p.Status == "ACTIVE" || p.Status == "LOCKED" || p.Status == "SOLD") && p.Author.Role != "Banned").Select(p => p.Id).ToListAsync()
                : await db.SpotSharingPosts.Where(p => ids.Contains(p.Id) && p.Status == "ACTIVE" && p.Author.Role != "Banned").Select(p => p.Id).ToListAsync();
            var reactions = await db.Reactions.Where(r => r.PostType == type && visible.Contains(r.PostId) && r.User.Role != "Banned")
                .GroupBy(r => r.PostId).Select(g => new { Id = g.Key, Count = g.Count(), Mine = userId.HasValue && g.Any(r => r.UserId == userId.Value) }).ToListAsync();
            var comments = await db.Comments.Where(c => c.PostType == type && visible.Contains(c.PostId) && !c.IsDeleted && c.Author.Role != "Banned")
                .GroupBy(c => c.PostId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync();
            foreach (var id in visible)
            {
                var reaction = reactions.FirstOrDefault(r => r.Id == id);
                result.Add(new() { PostId = id, PostType = type, ReactionCount = reaction?.Count ?? 0,
                    LikedByMe = reaction?.Mine ?? false, CommentCount = comments.FirstOrDefault(c => c.Id == id)?.Count ?? 0 });
            }
        }
        return result;
    }
    public async Task<CommunityInteractionSummary> SetLikeAsync(string type, int postId, int userId, bool liked)
    {
        type = Type(type);
        return await WriteAsync(type, postId, async () =>
        {
            await UserAsync(userId); await PublicPostAsync(type, postId);
            var existing = await db.Reactions.FirstOrDefaultAsync(r => r.PostType == type && r.PostId == postId && r.UserId == userId);
            if (liked && existing == null) db.Reactions.Add(new Reaction { PostType = type, PostId = postId, UserId = userId, ReactionType = "LIKE" });
            else if (liked && existing != null) existing.ReactionType = "LIKE";
            else if (!liked && existing != null) db.Reactions.Remove(existing);
            await db.SaveChangesAsync();
            return (await SummariesAsync(new[] { $"{type}:{postId}" }, userId)).Single();
        });
    }
    public async Task<CommunityCommentPage> CommentsAsync(string type, int postId, int page, int pageSize, int? parentId = null)
    {
        type = Type(type); await PublicPostAsync(type, postId);
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 50)
            throw new ServiceException(400, "Phan trang khong hop le.", "INVALID_PAGINATION");
        if (parentId.HasValue && !await db.Comments.AnyAsync(c => c.Id == parentId && c.PostType == type && c.PostId == postId && c.ParentCommentId == null))
            throw new ServiceException(404, "Khong tim thay binh luan goc.", "COMMENT_NOT_FOUND");
        var query = db.Comments.AsNoTracking().Include(c => c.Author)
            .Where(c => c.PostType == type && c.PostId == postId && c.ParentCommentId == parentId);
        // Keep tombstones so replies retain their original context; never expose deleted/banned text.
        var count = await query.CountAsync();
        var rows = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new() { Items = rows.Select(Map).ToList(), TotalCount = count, Page = page, PageSize = pageSize };
    }
    public async Task<CommunityCommentDto> CommentAsync(string type, int postId, int userId, CommunityCommentRequest request)
    {
        type = Type(type);
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Trim().Length > 1000 || request.ClientRequestId == Guid.Empty)
            throw new ServiceException(400, "Noi dung toi da 1000 ky tu va can ma gui hop le.", "INVALID_COMMENT");
        return await WriteAsync(type, postId, async () =>
        {
            await UserAsync(userId); await PublicPostAsync(type, postId);
            var existing = await db.Comments.Include(c => c.Author).FirstOrDefaultAsync(c => c.PostType == type && c.PostId == postId && c.AuthorId == userId && c.ClientRequestId == request.ClientRequestId);
            if (existing != null)
            {
                if (existing.Content != request.Content.Trim() || existing.ParentCommentId != request.ParentCommentId)
                    throw new ServiceException(409, "Ma gui da duoc dung cho noi dung khac.", "COMMENT_REQUEST_REUSED");
                return Map(existing);
            }
            if (request.ParentCommentId.HasValue)
            {
                var parent = await db.Comments.FirstOrDefaultAsync(c => c.Id == request.ParentCommentId && c.PostType == type && c.PostId == postId && c.Author.Role != "Banned");
                if (parent == null || parent.IsDeleted || parent.ParentCommentId.HasValue)
                    throw new ServiceException(400, "Chi tra loi binh luan goc con hoat dong cua dung bai viet.", "INVALID_COMMENT_PARENT");
            }
            var comment = new Comment { PostType = type, PostId = postId, AuthorId = userId, ParentCommentId = request.ParentCommentId,
                Content = request.Content.Trim(), ClientRequestId = request.ClientRequestId };
            db.Comments.Add(comment); await db.SaveChangesAsync();
            await db.Entry(comment).Reference(c => c.Author).LoadAsync();
            return Map(comment);
        });
    }
    public async Task DeleteCommentAsync(string type, int postId, int commentId, int userId)
    {
        type = Type(type);
        await WriteAsync(type, postId, async () =>
        {
            var user = await UserAsync(userId); await PublicPostAsync(type, postId);
            var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.PostType == type && c.PostId == postId)
                ?? throw new ServiceException(404, "Binh luan khong ton tai.", "COMMENT_NOT_FOUND");
            if (comment.AuthorId != userId && user.Role != "Admin") throw new ServiceException(403, "Khong co quyen xoa.", "FORBIDDEN");
            comment.IsDeleted = true; comment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(); return true;
        });
    }
    public async Task<CommunityModerationPost> ModerationPostAsync(int reportId, int userId)
    {
        await UserAsync(userId, true);
        var report = await db.PostReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId)
            ?? throw new ServiceException(404, "Bao cao khong ton tai.", "REPORT_NOT_FOUND");
        if (report.PostType == "PASS")
        {
            var p = await db.PassPosts.AsNoTracking().Include(p => p.Author).Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == report.PostId)
                ?? throw new ServiceException(404, "Bai viet khong ton tai.", "POST_NOT_FOUND");
            return new() { ReportId = reportId, PostId = p.Id, PostType = "PASS", Title = p.Title, Content = p.Description, Price = p.Price,
                Status = p.Status, AuthorName = p.Author.FullName, Images = p.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => new FeedImageDto { Id = i.Id, ImageUrl = i.ImageUrl, DisplayOrder = i.DisplayOrder }).ToList() };
        }
        var spot = await db.SpotSharingPosts.AsNoTracking().Include(p => p.Author).Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == report.PostId)
            ?? throw new ServiceException(404, "Bai viet khong ton tai.", "POST_NOT_FOUND");
        return new() { ReportId = reportId, PostId = spot.Id, PostType = "SPOT", Content = spot.Content, LocationName = spot.LocationName,
            Status = spot.Status, AuthorName = spot.Author.FullName, Images = spot.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => new FeedImageDto { Id = i.Id, ImageUrl = i.ImageUrl, DisplayOrder = i.DisplayOrder }).ToList() };
    }
    private static CommunityCommentDto Map(Comment c) => new() { Id = c.Id, PostId = c.PostId, PostType = c.PostType, AuthorId = c.AuthorId,
        AuthorName = c.Author?.Role == "Banned" ? "Thanh vien" : c.Author?.FullName ?? "Thanh vien", AvatarUrl = c.Author?.Role == "Banned" ? null : c.Author?.AvatarUrl,
        Content = c.IsDeleted || c.Author?.Role == "Banned" ? "Bình luận không còn hiển thị." : c.Content,
        ParentCommentId = c.ParentCommentId, IsDeleted = c.IsDeleted || c.Author?.Role == "Banned", CreatedAt = DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc) };
}
