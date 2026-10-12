using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Helpers;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;

namespace Doosii.BLL.Services
{
    public class CommunityFeedService : ICommunityFeedService
    {
        private const int MaxPageSize = 50;
        private static readonly HashSet<int> AllowedRadius = new() { 1, 3, 5, 10 };

        private readonly AppDbContext _db;
        private readonly ILogger<CommunityFeedService> _logger;

        public CommunityFeedService(AppDbContext db, ILogger<CommunityFeedService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<CommunityFeedResponseDto> GetFeedAsync(CommunityFeedQuery query)
        {
            // 1. Validate pagination
            if (query.Page < 1 || query.PageSize < 1)
            {
                throw new ServiceException(400, "Page va pageSize phai lon hon hoac bang 1.", "INVALID_PAGINATION");
            }

            var page = query.Page;
            var pageSize = Math.Min(query.PageSize, MaxPageSize);

            // 2. Validate tab
            var tab = string.IsNullOrWhiteSpace(query.Tab) ? "newest" : query.Tab.Trim().ToLowerInvariant();
            if (tab != "newest" && tab != "near-you" && tab != "by-style")
            {
                throw new ServiceException(400, "Tab khong hop le. Chi chap nhan newest, near-you, by-style.", "INVALID_TAB");
            }

            // 3. Validate radius neu duoc cung cap
            if (query.RadiusKm.HasValue && !AllowedRadius.Contains(query.RadiusKm.Value))
            {
                throw new ServiceException(400, "Ban kinh khong hop le. Chi chap nhan 1, 3, 5 hoac 10 km.", "INVALID_RADIUS");
            }

            // 4. Dispatch theo tung tab
            return tab switch
            {
                "newest" => await GetNewestFeedAsync(page, pageSize),
                "near-you" => await GetNearYouFeedAsync(query, page, pageSize),
                "by-style" => await GetByStyleFeedAsync(query.Style, page, pageSize),
                _ => throw new ServiceException(400, "Tab khong hop le.", "INVALID_TAB")
            };
        }

        // =========================================================================
        // TAB 1: NEWEST (Moi nhat tong hop giua PassPost va SpotSharingPost)
        // =========================================================================
        private async Task<CommunityFeedResponseDto> GetNewestFeedAsync(int page, int pageSize)
        {
            // Dem tong so bai ACTIVE cua ca 2 bang
            var passCount = await _db.PassPosts
                .AsNoTracking()
                .Where(p => p.Status == "ACTIVE")
                .CountAsync();

            var spotCount = await _db.SpotSharingPosts
                .AsNoTracking()
                .Where(s => s.Status == "ACTIVE")
                .CountAsync();

            var totalItems = passCount + spotCount;
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize);

            if (totalItems == 0 || page > totalPages)
            {
                return new CommunityFeedResponseDto
                {
                    Items = new List<CommunityFeedItemDto>(),
                    Tab = "newest",
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = totalPages
                };
            }

            // Lay toi da (page * pageSize) metadata (Id + CreatedAt) tu moi bang de merge chinh xac
            var maxCandidates = page * pageSize;

            var passMeta = await _db.PassPosts
                .AsNoTracking()
                .Where(p => p.Status == "ACTIVE")
                .OrderByDescending(p => p.CreatedAt)
                .Take(maxCandidates)
                .Select(p => new FeedItemMeta { Id = p.Id, Type = "PASS", CreatedAt = p.CreatedAt })
                .ToListAsync();

            var spotMeta = await _db.SpotSharingPosts
                .AsNoTracking()
                .Where(s => s.Status == "ACTIVE")
                .OrderByDescending(s => s.CreatedAt)
                .Take(maxCandidates)
                .Select(s => new FeedItemMeta { Id = s.Id, Type = "SPOT", CreatedAt = s.CreatedAt })
                .ToListAsync();

            // Gop va phan trang trong bo nho cho pham vi trang yeu cau
            var pageMeta = passMeta.Concat(spotMeta)
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var passIds = pageMeta.Where(m => m.Type == "PASS").Select(m => m.Id).ToList();
            var spotIds = pageMeta.Where(m => m.Type == "SPOT").Select(m => m.Id).ToList();

            // Truy van chi tiet cho dung so luong item cua trang do
            var passDict = passIds.Count == 0
                ? new Dictionary<int, PassPost>()
                : (await _db.PassPosts
                    .AsNoTracking()
                    .Include(p => p.Author)
                    .Include(p => p.Images)
                    .Where(p => passIds.Contains(p.Id))
                    .ToListAsync())
                    .ToDictionary(p => p.Id);

            var spotDict = spotIds.Count == 0
                ? new Dictionary<int, SpotSharingPost>()
                : (await _db.SpotSharingPosts
                    .AsNoTracking()
                    .Include(s => s.Author)
                    .Include(s => s.TaggedStore)
                    .Include(s => s.Images)
                    .Where(s => spotIds.Contains(s.Id))
                    .ToListAsync())
                    .ToDictionary(s => s.Id);

            var items = new List<CommunityFeedItemDto>();
            foreach (var meta in pageMeta)
            {
                if (meta.Type == "PASS" && passDict.TryGetValue(meta.Id, out var passPost))
                {
                    items.Add(MapPassPost(passPost));
                }
                else if (meta.Type == "SPOT" && spotDict.TryGetValue(meta.Id, out var spotPost))
                {
                    items.Add(MapSpotPost(spotPost, null));
                }
            }

            return new CommunityFeedResponseDto
            {
                Items = items,
                Tab = "newest",
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            };
        }

        // =========================================================================
        // TAB 2: NEAR-YOU (Loc theo toa do & ban kinh Haversine)
        // =========================================================================
        private async Task<CommunityFeedResponseDto> GetNearYouFeedAsync(CommunityFeedQuery query, int page, int pageSize)
        {
            if (!query.Lat.HasValue || !query.Lng.HasValue)
            {
                throw new ServiceException(400, "Latitude and longitude are required for near-you feed", "LOCATION_REQUIRED");
            }

            if (query.Lat.Value < -90 || query.Lat.Value > 90 || query.Lng.Value < -180 || query.Lng.Value > 180)
            {
                throw new ServiceException(400, "Latitude must be between -90 and 90, and longitude between -180 and 180.", "INVALID_COORDINATES");
            }

            var radius = query.RadiusKm ?? 5;
            if (!AllowedRadius.Contains(radius))
            {
                throw new ServiceException(400, "Radius must be 1, 3, 5, or 10 km.", "INVALID_RADIUS");
            }

            var userLat = query.Lat.Value;
            var userLng = query.Lng.Value;

            // 1. Loc so bo bang Bounding Box trong SQL
            var (minLat, maxLat, minLng, maxLng) = GeoCalculator.GetBoundingBox(userLat, userLng, radius);
            var minLatDec = (decimal)minLat;
            var maxLatDec = (decimal)maxLat;
            var minLngDec = (decimal)minLng;
            var maxLngDec = (decimal)maxLng;

            var candidateSpots = await _db.SpotSharingPosts
                .AsNoTracking()
                .Include(s => s.Author)
                .Include(s => s.TaggedStore)
                .Include(s => s.Images)
                .Where(s => s.Status == "ACTIVE" &&
                    (
                        // Toa do truc tiep tren bai viet
                        (s.Latitude != null && s.Latitude >= minLatDec && s.Latitude <= maxLatDec &&
                         s.Longitude != null && s.Longitude >= minLngDec && s.Longitude <= maxLngDec)
                        ||
                        // Toa do tu TaggedStore
                        (s.TaggedStore != null && s.TaggedStore.Latitude != null &&
                         s.TaggedStore.Latitude >= minLat && s.TaggedStore.Latitude <= maxLat &&
                         s.TaggedStore.Longitude != null &&
                         s.TaggedStore.Longitude >= minLng && s.TaggedStore.Longitude <= maxLng)
                    )
                )
                .ToListAsync();

            // 2. Tinh toan Haversine chinh xac trong bo nho
            var nearbyList = new List<(SpotSharingPost Post, double DistanceKm)>();
            foreach (var post in candidateSpots)
            {
                double? itemLat = post.Latitude.HasValue
                    ? (double)post.Latitude.Value
                    : post.TaggedStore?.Latitude;

                double? itemLng = post.Longitude.HasValue
                    ? (double)post.Longitude.Value
                    : post.TaggedStore?.Longitude;

                if (!itemLat.HasValue || !itemLng.HasValue) continue;

                var dist = GeoCalculator.CalculateDistanceKm(userLat, userLng, itemLat.Value, itemLng.Value);
                if (dist <= radius)
                {
                    nearbyList.Add((post, Math.Round(dist, 2)));
                }
            }

            // 3. Sap xep: gan nhat truoc, sau do CreatedAt DESC
            var sorted = nearbyList
                .OrderBy(x => x.DistanceKm)
                .ThenByDescending(x => x.Post.CreatedAt)
                .ToList();

            var totalItems = sorted.Count;
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize);

            var paged = sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = paged.Select(x => MapSpotPost(x.Post, x.DistanceKm)).ToList();

            return new CommunityFeedResponseDto
            {
                Items = items,
                Tab = "near-you",
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            };
        }

        // =========================================================================
        // TAB 3: BY-STYLE (Loc PassPost theo StyleTags)
        // =========================================================================
        private async Task<CommunityFeedResponseDto> GetByStyleFeedAsync(string? style, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(style))
            {
                throw new ServiceException(400, "Style parameter is required for by-style feed", "INVALID_STYLE");
            }

            var trimmedStyle = style.Trim();

            // Loc PassPost chua style tuong ung (khong phan biet chu hoa/chu thuong)
            var query = _db.PassPosts
                .AsNoTracking()
                .Where(p => p.Status == "ACTIVE" && p.StyleTags != null && p.StyleTags.ToLower().Contains(trimmedStyle.ToLower()));

            var totalItems = await query.CountAsync();
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize);

            if (totalItems == 0 || page > totalPages)
            {
                return new CommunityFeedResponseDto
                {
                    Items = new List<CommunityFeedItemDto>(),
                    Tab = "by-style",
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = totalPages
                };
            }

            var passPosts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(p => p.Author)
                .Include(p => p.Images)
                .ToListAsync();

            var items = passPosts.Select(MapPassPost).ToList();

            return new CommunityFeedResponseDto
            {
                Items = items,
                Tab = "by-style",
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            };
        }

        // =========================================================================
        // MAPPERS
        // =========================================================================
        private static CommunityFeedItemDto MapPassPost(PassPost p)
        {
            return new CommunityFeedItemDto
            {
                PostId = p.Id,
                PostType = "PASS",
                Title = p.Title,
                Content = p.Description,
                Price = p.Price,
                Size = p.Size,
                ConditionPercent = p.ConditionPercent,
                AllowEscrow = p.AllowEscrow,
                StyleTags = p.StyleTags,
                AuthorId = p.AuthorId,
                AuthorDisplayName = p.Author != null && !string.IsNullOrWhiteSpace(p.Author.FullName)
                    ? p.Author.FullName
                    : $"User #{p.AuthorId}",
                AuthorAvatarUrl = p.Author?.AvatarUrl,
                Images = p.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new FeedImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        DisplayOrder = i.DisplayOrder
                    })
                    .ToList(),
                TaggedStore = null,
                Latitude = null,
                Longitude = null,
                DistanceKm = null,
                CreatedAt = p.CreatedAt
            };
        }

        private static CommunityFeedItemDto MapSpotPost(SpotSharingPost s, double? distanceKm)
        {
            var effectiveLat = s.Latitude ?? (s.TaggedStore?.Latitude != null ? (decimal)s.TaggedStore.Latitude.Value : null);
            var effectiveLng = s.Longitude ?? (s.TaggedStore?.Longitude != null ? (decimal)s.TaggedStore.Longitude.Value : null);

            return new CommunityFeedItemDto
            {
                PostId = s.Id,
                PostType = "SPOT",
                Title = null,
                Content = s.Content,
                Price = null,
                Size = null,
                ConditionPercent = null,
                AllowEscrow = null,
                StyleTags = null,
                AuthorId = s.AuthorId,
                AuthorDisplayName = s.Author != null && !string.IsNullOrWhiteSpace(s.Author.FullName)
                    ? s.Author.FullName
                    : $"User #{s.AuthorId}",
                AuthorAvatarUrl = s.Author?.AvatarUrl,
                Images = s.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new FeedImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        DisplayOrder = i.DisplayOrder
                    })
                    .ToList(),
                TaggedStore = s.TaggedStore == null ? null : new FeedTaggedStoreDto
                {
                    Id = s.TaggedStore.Id,
                    Name = s.TaggedStore.Name,
                    Address = s.TaggedStore.Address,
                    Latitude = s.TaggedStore.Latitude,
                    Longitude = s.TaggedStore.Longitude
                },
                Latitude = effectiveLat,
                Longitude = effectiveLng,
                DistanceKm = distanceKm,
                CreatedAt = s.CreatedAt
            };
        }

        private class FeedItemMeta
        {
            public int Id { get; set; }
            public string Type { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }
    }
}
