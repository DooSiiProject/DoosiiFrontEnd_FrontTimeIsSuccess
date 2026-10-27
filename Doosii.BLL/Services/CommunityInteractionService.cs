using System.Data;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;
using Microsoft.EntityFrameworkCore;

namespace Doosii.BLL.Services;

public class CommunityInteractionService(AppDbContext db) : ICommunityInteractionService
{
    private async Task<string> PublicPostAsync(string type, int id)
    {
        var post = await CommunityAccess.PublicPosts(db).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new ServiceException(404, "Không tìm thấy bài viết công khai.", "POST_NOT_FOUND");
        var requested = type?.Trim().ToUpperInvariant();
        if (requested == "SPOT") requested = "OTHER"; // Transitional route alias only.
        if (!string.IsNullOrEmpty(requested) && requested != post.Type)
            throw new ServiceException(404, "Loại bài không khớp.", "POST_NOT_FOUND");
        return post.Type;
    }
    private async Task<T> WriteAsync<T>(int postId, Func<Task<T>> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await CommunityAccess.LockAsync(db, $"community:post:{postId}");
        var result = await action(); await tx.CommitAsync(); return result;
    }
    public async Task EnsureAdminAsync(int userId) => await CommunityAccess.ActorAsync(db, userId, true);
    public async Task<List<CommunityInteractionSummary>> SummariesAsync(string[] keys, int? userId)
    {
        if (keys == null || keys.Length > 50) throw new ServiceException(400, "Tối đa 50 bài mỗi yêu cầu.", "INVALID_POST_KEYS");
        if (userId.HasValue) await CommunityAccess.ActorAsync(db, userId.Value);
        var parsed = new List<(int Id, string Type)>();
        foreach (var key in keys.Distinct())
        {
            var parts = key.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var id) || id <= 0 || parts[0].ToUpperInvariant() is not ("PASS" or "OTHER" or "SPOT"))
                throw new ServiceException(400, "Định danh bài không hợp lệ.", "INVALID_POST_KEYS");
            parsed.Add((id, parts[0].ToUpperInvariant() == "SPOT" ? "OTHER" : parts[0].ToUpperInvariant()));
        }
        var ids = parsed.Select(p => p.Id).Distinct().ToArray();
        var posts = await CommunityAccess.PublicPosts(db).Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.Type }).ToListAsync();
        posts = posts.Where(p => parsed.Contains((p.Id, p.Type))).ToList();
        ids = posts.Select(p => p.Id).ToArray();
        var likes = await db.Reactions.Where(r => ids.Contains(r.PostId) && r.User.Role != "Banned").GroupBy(r => r.PostId)
            .Select(g => new { Id = g.Key, Count = g.Count(), Mine = userId.HasValue && g.Any(r => r.UserId == userId) }).ToDictionaryAsync(g => g.Id);
        var comments = await db.Comments.Where(c => ids.Contains(c.PostId) && !c.IsDeleted && c.Author.Role != "Banned").GroupBy(c => c.PostId)
            .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id);
        return posts.Select(p => new CommunityInteractionSummary { PostId = p.Id, PostType = p.Type, ReactionCount = likes.GetValueOrDefault(p.Id)?.Count ?? 0,
            LikedByMe = likes.GetValueOrDefault(p.Id)?.Mine ?? false, CommentCount = comments.GetValueOrDefault(p.Id)?.Count ?? 0 }).ToList();
    }
    public Task<CommunityInteractionSummary> SetLikeAsync(string type, int postId, int userId, bool liked) => WriteAsync(postId, async () =>
    {
        await CommunityAccess.ActorAsync(db, userId); type = await PublicPostAsync(type, postId);
        var reaction = await db.Reactions.SingleOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);
        if (liked && reaction == null) db.Reactions.Add(new() { PostId = postId, UserId = userId, ReactionType = "LIKE" });
        else if (liked) reaction!.ReactionType = "LIKE";
        else if (reaction != null) db.Reactions.Remove(reaction);
        await db.SaveChangesAsync(); return (await SummariesAsync(new[] { $"{type}:{postId}" }, userId)).Single();
    });
    public async Task<CommunityCommentPage> CommentsAsync(string type, int postId, int page, int pageSize, int? parentId = null)
    {
        type = await PublicPostAsync(type, postId); CommunityAccess.Pagination(page, pageSize);
        if (parentId.HasValue && !await db.Comments.AnyAsync(c => c.Id == parentId && c.PostId == postId && c.ParentCommentId == null))
            throw new ServiceException(404, "Không tìm thấy bình luận gốc.", "COMMENT_NOT_FOUND");
        var query = db.Comments.AsNoTracking().Include(c => c.Author).Where(c => c.PostId == postId && c.ParentCommentId == parentId);
        var count = await query.CountAsync();
        var rows = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new() { Items = rows.Select(c => Map(c, type)).ToList(), TotalCount = count, Page = page, PageSize = pageSize };
    }
    public Task<CommunityCommentDto> CommentAsync(string type, int postId, int userId, CommunityCommentRequest request) => WriteAsync(postId, async () =>
    {
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Trim().Length > 1000 || request.ClientRequestId == Guid.Empty)
            throw new ServiceException(400, "Bình luận tối đa 1000 ký tự và cần mã gửi hợp lệ.", "INVALID_COMMENT");
        await CommunityAccess.ActorAsync(db, userId); type = await PublicPostAsync(type, postId);
        var existing = await db.Comments.Include(c => c.Author).SingleOrDefaultAsync(c => c.PostId == postId && c.AuthorId == userId && c.ClientRequestId == request.ClientRequestId);
        if (existing != null)
        {
            if (existing.Content != request.Content.Trim() || existing.ParentCommentId != request.ParentCommentId) throw new ServiceException(409, "Mã gửi đã được dùng cho nội dung khác.", "COMMENT_REQUEST_REUSED");
            return Map(existing, type);
        }
        if (request.ParentCommentId.HasValue && !await db.Comments.AnyAsync(c => c.Id == request.ParentCommentId && c.PostId == postId && c.ParentCommentId == null && !c.IsDeleted && c.Author.Role != "Banned"))
            throw new ServiceException(400, "Chỉ trả lời bình luận gốc đang hoạt động của đúng bài.", "INVALID_COMMENT_PARENT");
        var comment = new Comment { PostId = postId, AuthorId = userId, Content = request.Content.Trim(), ParentCommentId = request.ParentCommentId, ClientRequestId = request.ClientRequestId };
        db.Comments.Add(comment); await db.SaveChangesAsync(); await db.Entry(comment).Reference(c => c.Author).LoadAsync(); return Map(comment, type);
    });
    public async Task DeleteCommentAsync(string type, int postId, int commentId, int userId)
    {
        await WriteAsync(postId, async () =>
        {
            var actor = await CommunityAccess.ActorAsync(db, userId); await PublicPostAsync(type, postId);
            var comment = await db.Comments.SingleOrDefaultAsync(c => c.Id == commentId && c.PostId == postId) ?? throw new ServiceException(404, "Không tìm thấy bình luận.", "COMMENT_NOT_FOUND");
            if (comment.AuthorId != userId && actor.Role != "Admin") throw new ServiceException(403, "Không có quyền xóa bình luận.", "FORBIDDEN");
            comment.IsDeleted = true; comment.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return true;
        });
    }
    public async Task<CommunityModerationPost> ModerationPostAsync(int reportId, int userId)
    {
        await CommunityAccess.ActorAsync(db, userId, true);
        var report = await db.PostReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reportId) ?? throw new ServiceException(404, "Không tìm thấy báo cáo.", "REPORT_NOT_FOUND");
        var p = await db.Posts.AsNoTracking().Include(p => p.Author).Include(p => p.PassInfo).Include(p => p.Media).SingleAsync(p => p.Id == report.PostId);
        return new() { ReportId = reportId, PostId = p.Id, PostType = p.Type, Title = p.Title, Content = p.PostDescription, Price = p.PassInfo?.Price, Status = p.VisibilityStatus,
            AuthorName = p.Author.FullName, Images = p.Media.Where(m => m.MediaType == "IMAGE").OrderBy(m => m.DisplayOrder).Select(m => new FeedImageDto { Id = m.Id, ImageUrl = m.SourceUrl, DisplayOrder = m.DisplayOrder }).ToList(),
            Media = p.Media.OrderBy(m => m.DisplayOrder).Select(m => new CommunityMediaDto { Id = m.Id, MediaType = m.MediaType, SourceUrl = m.SourceUrl, ThumbnailUrl = m.ThumbnailUrl, Caption = m.Caption, DisplayOrder = m.DisplayOrder }).ToList() };
    }
    private static CommunityCommentDto Map(Comment c, string type) => new() { Id = c.Id, PostId = c.PostId, PostType = type, AuthorId = c.AuthorId,
        AuthorName = c.Author.Role == "Banned" ? "Thành viên" : c.Author.FullName, AvatarUrl = c.Author.Role == "Banned" ? null : c.Author.AvatarUrl,
        Content = c.IsDeleted || c.Author.Role == "Banned" ? "Bình luận không còn hiển thị." : c.Content, ParentCommentId = c.ParentCommentId,
        IsDeleted = c.IsDeleted || c.Author.Role == "Banned", CreatedAt = DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc) };
}
