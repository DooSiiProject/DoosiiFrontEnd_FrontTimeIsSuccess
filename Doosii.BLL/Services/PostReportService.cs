using System.Data;
using System.Text.Json;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;
using Microsoft.EntityFrameworkCore;

namespace Doosii.BLL.Services;

public class PostReportService(AppDbContext db) : IPostReportService
{
    private static readonly string[] Reasons = { "SCAM", "COUNTERFEIT", "OFFENSIVE", "PROHIBITED_CONTENT", "OTHER" };
    public async Task<PostReportResponseDto> CreateReportAsync(int postId, int reporterId, CreatePostReportRequest request)
    {
        var reason = (request.Reason ?? "").Trim().ToUpperInvariant();
        if (!Reasons.Contains(reason) || request.Description?.Length > 1000) throw new ServiceException(400, "Báo cáo không hợp lệ.", "INVALID_REPORT_REASON");
        var evidence = request.EvidenceUrls ?? new();
        if (evidence.Count > 5 || evidence.Any(u => string.IsNullOrWhiteSpace(u) || u.Length > 1000 || !Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            throw new ServiceException(400, "Bằng chứng không hợp lệ.", "INVALID_REPORT_EVIDENCE");
        var evidenceJson = evidence.Count == 0 ? null : JsonSerializer.Serialize(evidence);
        if (evidenceJson?.Length > 3000) throw new ServiceException(400, "Bằng chứng quá dài.", "INVALID_REPORT_EVIDENCE");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await CommunityAccess.LockAsync(db, $"community:post:{postId}");
        await CommunityAccess.ActorAsync(db, reporterId);
        var post = await CommunityAccess.PublicPosts(db).SingleOrDefaultAsync(p => p.Id == postId) ?? throw new ServiceException(404, "Không tìm thấy bài viết công khai.", "POST_NOT_FOUND");
        var requested = request.PostType?.Trim().ToUpperInvariant(); if (requested == "SPOT") requested = "OTHER";
        if (!string.IsNullOrEmpty(requested) && requested != post.Type) throw new ServiceException(404, "Loại bài không khớp.", "POST_NOT_FOUND");
        if (post.AuthorId == reporterId) throw new ServiceException(400, "Không được báo cáo bài của mình.", "CANNOT_REPORT_OWN_POST");
        if (await db.PostReports.AnyAsync(r => r.PostId == postId && r.ReporterId == reporterId && r.Status == "PENDING")) throw new ServiceException(409, "Đã có báo cáo đang chờ xử lý.", "REPORT_ALREADY_PENDING");
        var report = new PostReport { PostId = postId, ReporterId = reporterId, Reason = reason, Description = request.Description?.Trim(), EvidenceUrlsJson = evidenceJson };
        db.PostReports.Add(report); await db.SaveChangesAsync(); await tx.CommitAsync(); return await MapAsync(report.Id);
    }
    public async Task<PagedResult<PostReportResponseDto>> GetReportsQueueAsync(PostReportQuery request)
    {
        CommunityAccess.Pagination(request.Page, request.PageSize);
        var query = db.PostReports.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            if (!new[] { "PENDING", "REVIEWED", "DISMISSED", "ACTION_TAKEN" }.Contains(status)) throw new ServiceException(400, "Trạng thái báo cáo không hợp lệ.", "INVALID_REPORT_STATUS");
            query = query.Where(r => r.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            var reason = request.Reason.Trim().ToUpperInvariant();
            if (!Reasons.Contains(reason)) throw new ServiceException(400, "Lý do không hợp lệ.", "INVALID_REPORT_REASON");
            query = query.Where(r => r.Reason == reason);
        }
        var total = await query.CountAsync();
        var ids = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(r => r.Id).ToListAsync();
        return new() { TotalCount = total, Page = request.Page, PageSize = request.PageSize, Items = await MapManyAsync(ids) };
    }
    public async Task<PostReportResponseDto> ResolveReportAsync(int reportId, int adminId, ResolvePostReportRequest request)
    {
        var action = (request.Action ?? "").Trim().ToUpperInvariant();
        if (action is not ("DISMISS" or "HIDE_POST") || request.AdminNote?.Length > 2000) throw new ServiceException(400, "Chỉ hỗ trợ DISMISS hoặc HIDE_POST.", "INVALID_REPORT_ACTION");
        var id = await db.PostReports.AsNoTracking().Where(r => r.Id == reportId).Select(r => (int?)r.PostId).SingleOrDefaultAsync()
            ?? throw new ServiceException(404, "Không tìm thấy báo cáo.", "REPORT_NOT_FOUND");
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await CommunityAccess.LockAsync(db, $"community:post:{id}");
            await CommunityAccess.ActorAsync(db, adminId, true);
            var report = await db.PostReports.SingleAsync(r => r.Id == reportId);
            var status = action == "DISMISS" ? "DISMISSED" : "ACTION_TAKEN";
            if (report.Status != "PENDING")
            {
                if (report.Status != status) throw new ServiceException(409, "Báo cáo đã được xử lý bằng hành động khác.", "REPORT_ALREADY_RESOLVED");
            }
            else
            {
                if (action == "HIDE_POST")
                {
                    var post = await db.Posts.SingleAsync(p => p.Id == id);
                    post.VisibilityStatus = "HIDDEN"; post.HiddenByAdmin = true; post.UpdatedAt = DateTime.UtcNow;
                }
                report.Status = status; report.AdminNote = request.AdminNote?.Trim(); report.ReviewedByAdminId = adminId; report.ReviewedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            await tx.CommitAsync();
        }
        return await MapAsync(reportId);
    }
    private async Task<PostReportResponseDto> MapAsync(int id) => (await MapManyAsync(new List<int> { id })).Single();
    private async Task<List<PostReportResponseDto>> MapManyAsync(List<int> ids)
    {
        var reports = await db.PostReports.AsNoTracking().Include(r => r.Reporter).Include(r => r.ReviewedByAdmin).Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        var postIds = reports.Values.Select(r => r.PostId).Distinct().ToArray();
        var posts = await db.Posts.AsNoTracking().Include(p => p.Author).Where(p => postIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        return ids.Select(id => { var r = reports[id]; var p = posts[r.PostId]; return new PostReportResponseDto {
            Id = r.Id, PostId = r.PostId, PostType = p.Type, Reason = r.Reason, Description = r.Description,
            EvidenceUrls = r.EvidenceUrlsJson == null ? new() : JsonSerializer.Deserialize<List<string>>(r.EvidenceUrlsJson) ?? new(),
            Status = r.Status, CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc), ReviewedAt = r.ReviewedAt.HasValue ? DateTime.SpecifyKind(r.ReviewedAt.Value, DateTimeKind.Utc) : null,
            AdminNote = r.AdminNote, Reporter = new() { Id = r.ReporterId, FullName = r.Reporter.FullName, AvatarUrl = r.Reporter.AvatarUrl },
            ReviewedBy = r.ReviewedByAdmin == null ? null : new() { Id = r.ReviewedByAdmin.Id, FullName = r.ReviewedByAdmin.FullName, AvatarUrl = r.ReviewedByAdmin.AvatarUrl },
            Post = new() { Id = p.Id, PostType = p.Type, Title = p.Title, AuthorId = p.AuthorId, AuthorName = p.Author.FullName, Status = p.VisibilityStatus }
        }; }).ToList();
    }
}
