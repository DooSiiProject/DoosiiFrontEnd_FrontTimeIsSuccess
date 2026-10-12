using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;

namespace Doosii.BLL.Services
{
    public class PostReportService : IPostReportService
    {
        private readonly AppDbContext _context;

        private static readonly HashSet<string> ValidReasons = new(StringComparer.OrdinalIgnoreCase)
        {
            "SCAM",
            "COUNTERFEIT",
            "OFFENSIVE",
            "PROHIBITED_CONTENT",
            "OTHER"
        };

        private static readonly HashSet<string> ValidActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "DISMISS",
            "HIDE_POST",
            "DELETE_POST",
            "BAN_AUTHOR"
        };

        public PostReportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PostReportResponseDto> CreateReportAsync(int postId, int reporterId, CreatePostReportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PostType))
            {
                throw new ServiceException(400, "PostType is required", "POST_NOT_FOUND");
            }

            var postType = request.PostType.Trim().ToUpperInvariant();
            if (postType != "PASS" && postType != "SPOT")
            {
                throw new ServiceException(400, "Invalid post type. Must be PASS or SPOT", "POST_NOT_FOUND");
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new ServiceException(400, "Reason is required", "INVALID_REPORT_REASON");
            }

            var reason = request.Reason.Trim().ToUpperInvariant();
            if (!ValidReasons.Contains(reason))
            {
                throw new ServiceException(400, "Invalid report reason. Allowed: SCAM, COUNTERFEIT, OFFENSIVE, PROHIBITED_CONTENT, OTHER", "INVALID_REPORT_REASON");
            }

            if (request.Description?.Length > 1000)
            {
                throw new ServiceException(400, "Description cannot exceed 1000 characters", "INVALID_REPORT_REASON");
            }

            List<string>? cleanedEvidenceUrls = null;
            if (request.EvidenceUrls != null && request.EvidenceUrls.Count > 0)
            {
                if (request.EvidenceUrls.Count > 5)
                {
                    throw new ServiceException(400, "Evidence URLs cannot exceed 5 items", "INVALID_REPORT_REASON");
                }

                cleanedEvidenceUrls = new List<string>();
                foreach (var rawUrl in request.EvidenceUrls)
                {
                    var url = rawUrl?.Trim();
                    if (string.IsNullOrWhiteSpace(url) ||
                        !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        throw new ServiceException(400, $"Invalid evidence URL format: {rawUrl}", "INVALID_REPORT_REASON");
                    }
                    cleanedEvidenceUrls.Add(url);
                }
            }

            // Kiem tra bai viet ton tai va dang public/active
            int postAuthorId = 0;
            string postTitle = string.Empty;
            string postStatus = string.Empty;
            string authorName = string.Empty;

            if (postType == "PASS")
            {
                var passPost = await _context.PassPosts
                    .Include(p => p.Author)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (passPost == null || passPost.Status == "HIDDEN")
                {
                    throw new ServiceException(404, "Pass post not found or is hidden", "POST_NOT_FOUND");
                }

                if (passPost.AuthorId == reporterId)
                {
                    throw new ServiceException(400, "You cannot report your own post", "CANNOT_REPORT_OWN_POST");
                }

                postAuthorId = passPost.AuthorId;
                postTitle = passPost.Title;
                postStatus = passPost.Status;
                authorName = passPost.Author?.FullName ?? "Unknown";
            }
            else // SPOT
            {
                var spotPost = await _context.SpotSharingPosts
                    .Include(p => p.Author)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (spotPost == null || spotPost.Status == "HIDDEN")
                {
                    throw new ServiceException(404, "Spot sharing post not found or is hidden", "POST_NOT_FOUND");
                }

                if (spotPost.AuthorId == reporterId)
                {
                    throw new ServiceException(400, "You cannot report your own post", "CANNOT_REPORT_OWN_POST");
                }

                postAuthorId = spotPost.AuthorId;
                postTitle = !string.IsNullOrEmpty(spotPost.LocationName)
                    ? spotPost.LocationName
                    : (spotPost.Content.Length > 50 ? spotPost.Content[..50] + "..." : spotPost.Content);
                postStatus = spotPost.Status;
                authorName = spotPost.Author?.FullName ?? "Unknown";
            }

            // Kiem tra duplicate report dang PENDING
            var hasPending = await _context.PostReports.AnyAsync(r =>
                r.PostId == postId &&
                r.PostType == postType &&
                r.ReporterId == reporterId &&
                r.Status == "PENDING");

            if (hasPending)
            {
                throw new ServiceException(409, "You have already reported this post", "REPORT_ALREADY_EXISTS");
            }

            var report = new PostReport
            {
                PostId = postId,
                PostType = postType,
                ReporterId = reporterId,
                Reason = reason,
                Description = request.Description?.Trim(),
                EvidenceUrlsJson = cleanedEvidenceUrls != null && cleanedEvidenceUrls.Count > 0
                    ? JsonSerializer.Serialize(cleanedEvidenceUrls)
                    : null,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            _context.PostReports.Add(report);
            await _context.SaveChangesAsync();

            var reporter = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == reporterId);

            return new PostReportResponseDto
            {
                Id = report.Id,
                PostId = report.PostId,
                PostType = report.PostType,
                Reason = report.Reason,
                Description = report.Description,
                EvidenceUrls = cleanedEvidenceUrls ?? new List<string>(),
                Status = report.Status,
                CreatedAt = report.CreatedAt,
                ReviewedAt = report.ReviewedAt,
                AdminNote = report.AdminNote,
                Post = new PostSummaryDto
                {
                    Id = postId,
                    PostType = postType,
                    Title = postTitle,
                    AuthorId = postAuthorId,
                    AuthorName = authorName,
                    Status = postStatus
                },
                Reporter = new UserSummaryDto
                {
                    Id = reporterId,
                    FullName = reporter?.FullName ?? "Unknown",
                    AvatarUrl = reporter?.AvatarUrl
                },
                ReviewedBy = null
            };
        }

        public async Task<PagedResult<PostReportResponseDto>> GetReportsQueueAsync(PostReportQuery query)
        {
            var queryable = _context.PostReports.AsNoTracking();

            // Status filter: mac dinh PENDING
            var filterStatus = string.IsNullOrWhiteSpace(query.Status)
                ? "PENDING"
                : query.Status.Trim().ToUpperInvariant();

            queryable = queryable.Where(r => r.Status == filterStatus);

            // Reason filter
            if (!string.IsNullOrWhiteSpace(query.Reason))
            {
                var filterReason = query.Reason.Trim().ToUpperInvariant();
                queryable = queryable.Where(r => r.Reason == filterReason);
            }

            var totalCount = await queryable.CountAsync();

            int page = query.Page < 1 ? 1 : query.Page;
            int pageSize = query.PageSize < 1 ? 20 : (query.PageSize > 50 ? 50 : query.PageSize);

            var reports = await queryable
                .Include(r => r.Reporter)
                .Include(r => r.ReviewedByAdmin)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Lay thong tin post lien quan theo batch
            var passPostIds = reports.Where(r => r.PostType == "PASS").Select(r => r.PostId).Distinct().ToList();
            var spotPostIds = reports.Where(r => r.PostType == "SPOT").Select(r => r.PostId).Distinct().ToList();

            var passPosts = await _context.PassPosts
                .Include(p => p.Author)
                .Where(p => passPostIds.Contains(p.Id))
                .AsNoTracking()
                .ToDictionaryAsync(p => p.Id);

            var spotPosts = await _context.SpotSharingPosts
                .Include(p => p.Author)
                .Where(p => spotPostIds.Contains(p.Id))
                .AsNoTracking()
                .ToDictionaryAsync(p => p.Id);

            var items = reports.Select(r =>
            {
                List<string> evidenceUrls = new();
                if (!string.IsNullOrEmpty(r.EvidenceUrlsJson))
                {
                    try
                    {
                        evidenceUrls = JsonSerializer.Deserialize<List<string>>(r.EvidenceUrlsJson) ?? new();
                    }
                    catch
                    {
                        evidenceUrls = new();
                    }
                }

                PostSummaryDto postSummary;
                if (r.PostType == "PASS" && passPosts.TryGetValue(r.PostId, out var pass))
                {
                    postSummary = new PostSummaryDto
                    {
                        Id = pass.Id,
                        PostType = "PASS",
                        Title = pass.Title,
                        AuthorId = pass.AuthorId,
                        AuthorName = pass.Author?.FullName ?? "Unknown",
                        Status = pass.Status
                    };
                }
                else if (r.PostType == "SPOT" && spotPosts.TryGetValue(r.PostId, out var spot))
                {
                    postSummary = new PostSummaryDto
                    {
                        Id = spot.Id,
                        PostType = "SPOT",
                        Title = !string.IsNullOrEmpty(spot.LocationName)
                            ? spot.LocationName
                            : (spot.Content.Length > 50 ? spot.Content[..50] + "..." : spot.Content),
                        AuthorId = spot.AuthorId,
                        AuthorName = spot.Author?.FullName ?? "Unknown",
                        Status = spot.Status
                    };
                }
                else
                {
                    postSummary = new PostSummaryDto
                    {
                        Id = r.PostId,
                        PostType = r.PostType,
                        Title = "Post unavailable or removed",
                        AuthorId = 0,
                        AuthorName = "Unknown",
                        Status = "DELETED"
                    };
                }

                return new PostReportResponseDto
                {
                    Id = r.Id,
                    PostId = r.PostId,
                    PostType = r.PostType,
                    Reason = r.Reason,
                    Description = r.Description,
                    EvidenceUrls = evidenceUrls,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    ReviewedAt = r.ReviewedAt,
                    AdminNote = r.AdminNote,
                    Post = postSummary,
                    Reporter = new UserSummaryDto
                    {
                        Id = r.Reporter.Id,
                        FullName = r.Reporter.FullName,
                        AvatarUrl = r.Reporter.AvatarUrl
                    },
                    ReviewedBy = r.ReviewedByAdmin != null
                        ? new UserSummaryDto
                        {
                            Id = r.ReviewedByAdmin.Id,
                            FullName = r.ReviewedByAdmin.FullName,
                            AvatarUrl = r.ReviewedByAdmin.AvatarUrl
                        }
                        : null
                };
            }).ToList();

            return new PagedResult<PostReportResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PostReportResponseDto> ResolveReportAsync(int reportId, int adminId, ResolvePostReportRequest request)
        {
            var report = await _context.PostReports
                .Include(r => r.Reporter)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
            {
                throw new ServiceException(404, "Report not found", "REPORT_NOT_FOUND");
            }

            if (report.Status != "PENDING")
            {
                throw new ServiceException(400, "Report has already been resolved", "REPORT_ALREADY_RESOLVED");
            }

            if (string.IsNullOrWhiteSpace(request.Action))
            {
                throw new ServiceException(400, "Action is required", "INVALID_MODERATION_ACTION");
            }

            var action = request.Action.Trim().ToUpperInvariant();
            if (!ValidActions.Contains(action))
            {
                throw new ServiceException(400, "Invalid moderation action. Allowed: DISMISS, HIDE_POST, DELETE_POST, BAN_AUTHOR", "INVALID_MODERATION_ACTION");
            }

            // Su dung transaction theo yeu cau
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (action == "DISMISS")
                {
                    report.Status = "DISMISSED";
                }
                else if (action == "HIDE_POST" || action == "DELETE_POST")
                {
                    if (report.PostType == "PASS")
                    {
                        var pass = await _context.PassPosts.FirstOrDefaultAsync(p => p.Id == report.PostId);
                        if (pass != null)
                        {
                            pass.Status = "HIDDEN";
                        }
                    }
                    else if (report.PostType == "SPOT")
                    {
                        var spot = await _context.SpotSharingPosts.FirstOrDefaultAsync(p => p.Id == report.PostId);
                        if (spot != null)
                        {
                            spot.Status = "HIDDEN";
                        }
                    }
                    report.Status = "ACTION_TAKEN";
                }
                else if (action == "BAN_AUTHOR")
                {
                    // BAN_AUTHOR: User entity chua co cot IsBanned/Status trong thiet ke hien tai
                    // Danh dau ACTION_TAKEN va ghi nhan TODO cho co che User Ban
                    report.Status = "ACTION_TAKEN";
                }

                report.ReviewedByAdminId = adminId;
                report.AdminNote = request.AdminNote?.Trim();
                report.ReviewedAt = DateTime.UtcNow;

                // TODO: Ghi AdminAuditLogs khi entity/table duoc cung cap tu module cua Wee.

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // Lay thong tin admin reviewer va post summary
            var admin = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == adminId);

            PostSummaryDto postSummary;
            if (report.PostType == "PASS")
            {
                var pass = await _context.PassPosts.Include(p => p.Author).AsNoTracking().FirstOrDefaultAsync(p => p.Id == report.PostId);
                postSummary = new PostSummaryDto
                {
                    Id = report.PostId,
                    PostType = "PASS",
                    Title = pass?.Title ?? "Deleted or unavailable",
                    AuthorId = pass?.AuthorId ?? 0,
                    AuthorName = pass?.Author?.FullName ?? "Unknown",
                    Status = pass?.Status ?? "DELETED"
                };
            }
            else
            {
                var spot = await _context.SpotSharingPosts.Include(p => p.Author).AsNoTracking().FirstOrDefaultAsync(p => p.Id == report.PostId);
                postSummary = new PostSummaryDto
                {
                    Id = report.PostId,
                    PostType = "SPOT",
                    Title = !string.IsNullOrEmpty(spot?.LocationName)
                        ? spot.LocationName
                        : (spot?.Content != null && spot.Content.Length > 50 ? spot.Content[..50] + "..." : spot?.Content ?? "Deleted or unavailable"),
                    AuthorId = spot?.AuthorId ?? 0,
                    AuthorName = spot?.Author?.FullName ?? "Unknown",
                    Status = spot?.Status ?? "DELETED"
                };
            }

            List<string> evidenceUrls = new();
            if (!string.IsNullOrEmpty(report.EvidenceUrlsJson))
            {
                try
                {
                    evidenceUrls = JsonSerializer.Deserialize<List<string>>(report.EvidenceUrlsJson) ?? new();
                }
                catch
                {
                    evidenceUrls = new();
                }
            }

            return new PostReportResponseDto
            {
                Id = report.Id,
                PostId = report.PostId,
                PostType = report.PostType,
                Reason = report.Reason,
                Description = report.Description,
                EvidenceUrls = evidenceUrls,
                Status = report.Status,
                CreatedAt = report.CreatedAt,
                ReviewedAt = report.ReviewedAt,
                AdminNote = report.AdminNote,
                Post = postSummary,
                Reporter = new UserSummaryDto
                {
                    Id = report.Reporter.Id,
                    FullName = report.Reporter.FullName,
                    AvatarUrl = report.Reporter.AvatarUrl
                },
                ReviewedBy = admin != null
                    ? new UserSummaryDto
                    {
                        Id = admin.Id,
                        FullName = admin.FullName,
                        AvatarUrl = admin.AvatarUrl
                    }
                    : null
            };
        }
    }
}
