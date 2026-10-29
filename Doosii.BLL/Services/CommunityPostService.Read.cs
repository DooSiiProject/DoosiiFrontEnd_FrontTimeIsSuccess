using System.Text.Json;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Doosii.BLL.Services;

public partial class CommunityPostService(AppDbContext db, ICommunityMediaStorage storage, IConfiguration configuration, ILogger<CommunityPostService> logger) : ICommunityPostService
{
    private IQueryable<Post> Loaded() => db.Posts.Include(p => p.Author).Include(p => p.PassInfo)
        .Include(p => p.Media).ThenInclude(m => m.Store).ThenInclude(s => s!.Owner).ThenInclude(u => u.MerchantProfile)
        .Include(p => p.Media).ThenInclude(m => m.Product).AsSplitQuery();

    public async Task<CommunityPostDto> DetailAsync(int postId, int? viewerId, bool moderation = false)
    {
        if (moderation) await CommunityAccess.ActorAsync(db, viewerId ?? 0, true);
        else if (viewerId.HasValue) await CommunityAccess.ActorAsync(db, viewerId.Value);
        var post = await Loaded().AsNoTracking().SingleOrDefaultAsync(p => p.Id == postId)
            ?? throw new ServiceException(404, "Không tìm thấy bài viết.", "POST_NOT_FOUND");
        if (!moderation && (post.Author.Role == "Banned" || post.VisibilityStatus != "ACTIVE" && post.AuthorId != viewerId))
            throw new ServiceException(404, "Không tìm thấy bài viết.", "POST_NOT_FOUND");
        return (await MapAsync(new List<Post> { post }, viewerId)).Single();
    }

    public async Task<CommunityPage<CommunityPostDto>> FeedAsync(CommunityPostQuery request, int? viewerId)
    {
        CommunityAccess.Pagination(request.Page, request.PageSize);
        if (viewerId.HasValue) await CommunityAccess.ActorAsync(db, viewerId.Value);
        if (request.Tab is not ("newest" or "by-style" or "near-you")) throw new ServiceException(400, "Tab không hợp lệ.", "INVALID_FEED_TAB");
        var query = CommunityAccess.PublicPosts(db).AsNoTracking();
        if (request.StoreId.HasValue)
        {
            if (!await CommunityAccess.PublicStores(db).AnyAsync(s => s.Id == request.StoreId.Value)) throw new ServiceException(404, "Không tìm thấy shop.", "STORE_NOT_FOUND");
            query = query.Where(p => p.Type == "OTHER" && p.Media.Any(m => m.StoreId == request.StoreId && (m.ProductId == null || m.Product!.IsPublished)));
        }
        if (request.Tab == "by-style")
        {
            var style = (request.Style ?? "").Trim();
            if (!CommunityPostRules.StyleTags.Contains(style)) throw new ServiceException(400, "Phong cách không hợp lệ.", "INVALID_STYLE_TAGS");
            var quoted = JsonSerializer.Serialize(style);
            query = query.Where(p => p.Type == "PASS" && p.PassInfo != null && p.PassInfo.StyleTagsJson.Contains(quoted));
        }
        var ranked = query.Select(p => new { p.Id, p.CreatedAt, Distance = (double?)null });
        if (request.Tab == "near-you")
        {
            if (!request.Latitude.HasValue || !request.Longitude.HasValue || !double.IsFinite(request.Latitude.Value) || !double.IsFinite(request.Longitude.Value) || Math.Abs(request.Latitude.Value) > 90 || Math.Abs(request.Longitude.Value) > 180 || !double.IsFinite(request.RadiusKm) || request.RadiusKm is <= 0 or > 50)
                throw new ServiceException(400, "Tọa độ hoặc bán kính không hợp lệ.", "INVALID_COORDINATES");
            var lat = request.Latitude.Value * Math.PI / 180; var lng = request.Longitude.Value * Math.PI / 180;
            var distances = db.Database.SqlQuery<CommunityNearbyRow>($"""
                SELECT m.PostId, MIN(6371.0 * ACOS(CASE WHEN c.Cosine > 1 THEN 1 WHEN c.Cosine < -1 THEN -1 ELSE c.Cosine END)) AS DistanceKm
                FROM forum.PostMedia m
                JOIN store.Stores s ON s.Id=m.StoreId
                JOIN auth.Users u ON u.Id=s.OwnerId
                JOIN auth.MerchantProfiles k ON k.UserId=u.Id
                LEFT JOIN store.Products product ON product.Id=m.ProductId
                CROSS APPLY (SELECT SIN({lat}) * SIN(RADIANS(s.Latitude)) + COS({lat}) * COS(RADIANS(s.Latitude)) * COS(RADIANS(s.Longitude) - {lng}) AS Cosine) c
                WHERE m.PostType='OTHER' AND s.IsActive=1 AND u.Role='Shopper' AND k.KycStatus='APPROVED'
                  AND s.Latitude BETWEEN -90 AND 90 AND s.Longitude BETWEEN -180 AND 180
                  AND (m.ProductId IS NULL OR product.IsPublished=1)
                GROUP BY m.PostId
                """);
            ranked = query.Where(p => p.Type == "OTHER").Join(distances, p => p.Id, d => d.PostId,
                (p, d) => new { p.Id, p.CreatedAt, Distance = (double?)d.DistanceKm }).Where(p => p.Distance <= request.RadiusKm);
        }
        var total = await ranked.CountAsync();
        var selected = await ranked.OrderBy(p => p.Distance).ThenByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync();
        var ids = selected.Select(p => p.Id).ToArray();
        // Recheck visibility in case moderation ran between the ID and hydration queries.
        var posts = await Loaded().AsNoTracking().Where(p => ids.Contains(p.Id) && p.VisibilityStatus == "ACTIVE" && p.Author.Role != "Banned").ToListAsync();
        var mapped = (await MapAsync(posts, viewerId)).ToDictionary(p => p.Id);
        return new() { TotalItems = total, Page = request.Page, PageSize = request.PageSize,
            Items = selected.Where(p => mapped.ContainsKey(p.Id)).Select(p => { var item = mapped[p.Id]; item.DistanceKm = p.Distance; return item; }).ToList() };
    }
    private async Task<List<CommunityPostDto>> MapAsync(List<Post> posts, int? viewerId)
    {
        var ids = posts.Select(p => p.Id).ToArray();
        var reactions = await db.Reactions.Where(r => ids.Contains(r.PostId) && r.User.Role != "Banned").GroupBy(r => r.PostId)
            .Select(g => new { Id = g.Key, Count = g.Count(), Mine = viewerId.HasValue && g.Any(r => r.UserId == viewerId) }).ToDictionaryAsync(r => r.Id);
        var comments = await db.Comments.Where(c => ids.Contains(c.PostId) && !c.IsDeleted && c.Author.Role != "Banned").GroupBy(c => c.PostId)
            .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(c => c.Id);
        var reserved = (await db.Set<Doosii.DAL.Models.Forum.PassOrderLock>().AsNoTracking().Where(l => ids.Contains(l.PostId)).Select(l => l.PostId).ToListAsync()).ToHashSet();
        return posts.Select(p => new CommunityPostDto {
            Id = p.Id, Type = p.Type, Author = new() { Id = p.AuthorId, FullName = p.Author.FullName, AvatarUrl = p.Author.AvatarUrl },
            Title = p.Title, PostDescription = p.PostDescription, VisibilityStatus = p.VisibilityStatus, HiddenByAdmin = p.HiddenByAdmin,
            Version = Convert.ToBase64String(p.RowVersion), CreatedAt = Utc(p.CreatedAt), UpdatedAt = p.UpdatedAt.HasValue ? Utc(p.UpdatedAt.Value) : null,
            SaleStatus = p.PassInfo?.SaleStatus,
            IsPassReserved = p.Type == "PASS" && reserved.Contains(p.Id),
            PassInfo = p.PassInfo == null ? null : new() { ProductName = p.PassInfo.ProductName, ProductDescription = p.PassInfo.ProductDescription,
                Price = p.PassInfo.Price, Size = p.PassInfo.Size, ConditionPercent = p.PassInfo.ConditionPercent, Defects = p.PassInfo.Defects,
                StyleTagIds = JsonSerializer.Deserialize<List<string>>(p.PassInfo.StyleTagsJson) ?? new(),
                Measurements = p.PassInfo.MeasurementsJson == null ? null : JsonSerializer.Deserialize<List<ProductMeasurement>>(p.PassInfo.MeasurementsJson) },
            Summary = new() { PostId = p.Id, PostType = p.Type, ReactionCount = reactions.GetValueOrDefault(p.Id)?.Count ?? 0,
                LikedByMe = reactions.GetValueOrDefault(p.Id)?.Mine ?? false, CommentCount = comments.GetValueOrDefault(p.Id)?.Count ?? 0 },
            Media = p.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(MapMedia).ToList()
        }).ToList();
    }
    private static CommunityMediaDto MapMedia(PostMedia m)
    {
        var s = m.Store;
        bool visible = s != null && s.IsActive && s.Owner.Role == "Shopper" && s.Owner.MerchantProfile?.KycStatus == "APPROVED" && (m.ProductId == null || m.Product?.IsPublished == true);
        return new() { Id = m.Id, MediaType = m.MediaType, SourceUrl = m.SourceUrl, ThumbnailUrl = m.ThumbnailUrl, Caption = m.Caption, DisplayOrder = m.DisplayOrder,
            AttachmentUnavailable = m.StoreId.HasValue && !visible,
            Store = visible ? new(s!.Id, s.Name, s.Latitude, s.Longitude) : null,
            Product = visible && m.Product != null ? new(m.Product.Id, m.Product.Title, m.Product.Price, m.Product.Status) : null };
    }
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public class CommunityNearbyRow
{
    public int PostId { get; set; }
    public double DistanceKm { get; set; }
}
