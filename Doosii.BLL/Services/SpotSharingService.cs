using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;
using Store = Doosii.DAL.Models.Store.Store;

namespace Doosii.BLL.Services
{
    public class SpotSharingService : ISpotSharingService
    {
        private const int MaxImagesPerPost = 5;
        private const int MinImagesPerPost = 1;
        private const int MaxContentLength = 2000;
        private const string UploadFolder = "forum/spots";
        private const int MaxPageSize = 50;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private readonly AppDbContext _db;
        private readonly ICloudinaryService _cloudinary;
        private readonly ILogger<SpotSharingService> _logger;

        public SpotSharingService(
            AppDbContext db,
            ICloudinaryService cloudinary,
            ILogger<SpotSharingService> logger)
        {
            _db = db;
            _cloudinary = cloudinary;
            _logger = logger;
        }

        /// <summary>
        /// Tao moi bai Spot Sharing:
        /// 1. Validate author ton tai.
        /// 2. Validate location: TaggedStoreId hoac (Lat + Lng) phai co it nhat 1.
        /// 3. Neu co TaggedStoreId: kiem tra Store ton tai va IsActive = true.
        /// 4. Neu co toa do thu cong: kiem tra Lat [-90,90] va Lng [-180,180].
        /// 5. Validate content va images (dinh dang, so luong).
        /// 6. Kiem tra Cloudinary da cau hinh.
        /// 7. Upload anh - rollback neu loi.
        /// 8. Ghi database trong transaction - rollback Cloudinary neu loi.
        /// </summary>
        public async Task<CreateSpotPostResponse> CreateSpotPostAsync(int authorId, CreateSpotPostRequest request)
        {
            // ---- 0. Verify Author ----
            var author = await _db.Users.FindAsync(authorId);
            if (author == null)
                throw new ServiceException(401, "Tai khoan khong hop le.", "UNAUTHORIZED");

            if (author.Role == "Banned") throw new ServiceException(403, "Tai khoan da bi khoa.", "USER_BANNED");
            CommunityPostingRules.ValidateImages(request.Images);

            // ---- 1. Location Validation ----
            bool hasStore = request.TaggedStoreId.HasValue;
            bool hasCoords = request.Latitude.HasValue && request.Longitude.HasValue;

            if (!hasStore && !hasCoords)
                throw new ServiceException(400,
                    "Bai dang phai co TaggedStoreId hoac cap toa do Latitude/Longitude.",
                    "LOCATION_REQUIRED");

            // Neu chi co 1 toa do thi yeu cau ca 2
            if (request.Latitude.HasValue && !request.Longitude.HasValue ||
                !request.Latitude.HasValue && request.Longitude.HasValue)
                throw new ServiceException(400,
                    "Phai cung cap ca Latitude va Longitude khi su dung toa do thu cong.",
                    "INVALID_COORDINATES");

            // Validate coordinates range
            if (hasCoords)
            {
                if (request.Latitude!.Value < -90 || request.Latitude.Value > 90)
                    throw new ServiceException(400,
                        "Latitude phai nam trong khoang tu -90 den 90.",
                        "INVALID_COORDINATES");

                if (request.Longitude!.Value < -180 || request.Longitude.Value > 180)
                    throw new ServiceException(400,
                        "Longitude phai nam trong khoang tu -180 den 180.",
                        "INVALID_COORDINATES");
            }

            // ---- 2. Validate Tagged Store (neu co) ----
            Store? taggedStore = null;
            if (hasStore)
            {
                taggedStore = await _db.Stores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == request.TaggedStoreId!.Value && s.IsActive && s.Owner.Role == "Shopper" && s.Owner.MerchantProfile != null && s.Owner.MerchantProfile.KycStatus == "APPROVED");

                if (taggedStore == null)
                    throw new ServiceException(404,
                        $"Khong tim thay Store voi Id = {request.TaggedStoreId}.",
                        "STORE_NOT_FOUND");

                if (!taggedStore.IsActive)
                    throw new ServiceException(400,
                        "Store nay hien tai dang khong hoat dong.",
                        "STORE_NOT_ACTIVE");
            }

            // ---- 3. Content Validation ----
            if (string.IsNullOrWhiteSpace(request.Content))
                throw new ServiceException(400, "Content la bat buoc.", "INVALID_CONTENT");

            if (request.Content.Trim().Length > MaxContentLength)
                throw new ServiceException(400,
                    $"Content toi da {MaxContentLength} ky tu.",
                    "INVALID_CONTENT");

            if (request.LocationName != null && request.LocationName.Length > 200)
                throw new ServiceException(400,
                    "LocationName toi da 200 ky tu.",
                    "INVALID_CONTENT");

            // ---- 4. Image Validation ----
            if (request.Images == null || request.Images.Count < MinImagesPerPost)
                throw new ServiceException(400,
                    $"Bai dang can it nhat {MinImagesPerPost} anh.",
                    "INVALID_IMAGE_COUNT");

            if (request.Images.Count > MaxImagesPerPost)
                throw new ServiceException(400,
                    $"Bai dang chi duoc toi da {MaxImagesPerPost} anh.",
                    "INVALID_IMAGE_COUNT");

            // Validate tat ca file anh TRUOC khi upload bat ky anh nao
            foreach (var img in request.Images)
            {
                if (img == null || img.Length == 0)
                    throw new ServiceException(400, "File anh khong hop le hoac trong.", "EMPTY_FILE");

                var ext = Path.GetExtension(img.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                    throw new ServiceException(400,
                        $"Dinh dang file '{ext}' khong duoc ho tro. Chi chap nhan .jpg, .jpeg, .png, .webp.",
                        "INVALID_IMAGE_FORMAT");
            }

            // ---- 5. Cloudinary Config Check ----
            if (!_cloudinary.IsConfigured)
            {
                _logger.LogWarning("Cloudinary not configured; cannot create SpotPost for user {AuthorId}", authorId);
                throw new ServiceException(503,
                    "Dich vu upload anh chua duoc cau hinh.",
                    "CLOUDINARY_NOT_CONFIGURED");
            }

            // ---- 6. Upload Images (with rollback on partial failure) ----
            var uploadedImages = new List<ImageUploadResult>();
            try
            {
                for (int i = 0; i < request.Images.Count; i++)
                {
                    var file = request.Images[i];
                    _logger.LogInformation("Uploading spot image {Index}/{Total} for user {AuthorId}",
                        i + 1, request.Images.Count, authorId);
                    var result = await _cloudinary.UploadImageAsync(file, UploadFolder);
                    uploadedImages.Add(result);
                }
            }
            catch (ServiceException)
            {
                await RollbackUploadedImagesAsync(uploadedImages);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during spot image upload for user {AuthorId}", authorId);
                await RollbackUploadedImagesAsync(uploadedImages);
                throw new ServiceException(502, "Upload anh that bai, vui long thu lai.", "UPLOAD_FAILED");
            }

            // ---- 7. Database Transaction ----
            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var liveAuthor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == authorId);
                if (liveAuthor == null || liveAuthor.Role == "Banned") throw new ServiceException(403, "Tai khoan khong the dang bai.", "USER_BANNED");
                if (request.TaggedStoreId.HasValue && !await _db.Stores.AnyAsync(s => s.Id == request.TaggedStoreId && s.IsActive && s.Owner.Role == "Shopper" && s.Owner.MerchantProfile != null && s.Owner.MerchantProfile.KycStatus == "APPROVED"))
                    throw new ServiceException(404, "Shop khong con cong khai.", "STORE_NOT_FOUND");
                var spotPost = new SpotSharingPost
                {
                    AuthorId = authorId,
                    TaggedStoreId = request.TaggedStoreId,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    LocationName = request.LocationName?.Trim(),
                    Content = request.Content.Trim(),
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow
                };

                _db.SpotSharingPosts.Add(spotPost);
                await _db.SaveChangesAsync();

                var spotImages = uploadedImages
                    .Select((img, idx) => new SpotImage
                    {
                        SpotPostId = spotPost.Id,
                        ImageUrl = img.SecureUrl,
                        PublicId = img.PublicId,
                        DisplayOrder = idx
                    })
                    .ToList();

                _db.SpotImages.AddRange(spotImages);
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "SpotPost {PostId} created by user {AuthorId} with {ImageCount} image(s), Store={StoreId}",
                    spotPost.Id, authorId, spotImages.Count, request.TaggedStoreId);

                return new CreateSpotPostResponse
                {
                    Id = spotPost.Id,
                    Content = spotPost.Content,
                    LocationName = spotPost.LocationName,
                    Status = spotPost.Status,
                    CreatedAt = spotPost.CreatedAt,
                    Author = new SpotPostAuthorDto
                    {
                        Id = author.Id,
                        FullName = author.FullName,
                        AvatarUrl = author.AvatarUrl
                    },
                    TaggedStore = taggedStore == null ? null : new SpotPostTaggedStoreDto
                    {
                        Id = taggedStore.Id,
                        Name = taggedStore.Name,
                        Address = taggedStore.Address,
                        Latitude = taggedStore.Latitude,
                        Longitude = taggedStore.Longitude
                    },
                    Latitude = spotPost.Latitude,
                    Longitude = spotPost.Longitude,
                    Images = spotImages.Select(img => new SpotImageDto
                    {
                        Id = img.Id,
                        ImageUrl = img.ImageUrl,
                        DisplayOrder = img.DisplayOrder
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Database error creating SpotPost for user {AuthorId}, rolling back {Count} Cloudinary image(s)",
                    authorId, uploadedImages.Count);

                // Xoa anh tren Cloudinary vi database insert that bai
                await RollbackUploadedImagesAsync(uploadedImages);

                if (ex is ServiceException) throw;
                throw new ServiceException(500, "Tao bai dang that bai do loi he thong.", "INTERNAL_ERROR");
            }
        }

        /// <summary>
        /// Lay chi tiet bai Spot Post theo ID.
        /// Chi tra ve bai cong khai cua tai khoan dang hoat dong.
        /// </summary>
        public async Task<SpotPostDetailDto?> GetSpotPostByIdAsync(int postId)
        {
            var post = await _db.SpotSharingPosts
                .Include(p => p.Author)
                .Include(p => p.TaggedStore)
                .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                .FirstOrDefaultAsync(p => p.Id == postId && p.Status == "ACTIVE" && p.Author.Role != "Banned");

            if (post == null) return null;

            return MapToDetailDto(post);
        }

        /// <summary>
        /// Lay danh sach Spot Post theo storeId co phan trang, moi nhat truoc.
        /// </summary>
        public async Task<SpotPostPagedResult> GetSpotPostsByStoreAsync(int storeId, int page, int pageSize)
        {
            // Kiem tra store ton tai
            if (page < 1 || page > 1000000 || pageSize < 1 || pageSize > MaxPageSize) throw new ServiceException(400, "Invalid pagination.", "INVALID_QUERY");
            var storeExists = await _db.Stores.AnyAsync(s => s.Id == storeId && s.IsActive && s.Owner.Role == "Shopper" && s.Owner.MerchantProfile != null && s.Owner.MerchantProfile.KycStatus == "APPROVED");
            if (!storeExists)
                throw new ServiceException(404,
                    $"Khong tim thay Store voi Id = {storeId}.",
                    "STORE_NOT_FOUND");

            // Gioi han pageSize toi da 50
            pageSize = Math.Min(pageSize, MaxPageSize);
            page = Math.Max(page, 1);

            var query = _db.SpotSharingPosts
                .Include(p => p.Author)
                .Include(p => p.TaggedStore)
                .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                .AsNoTracking()
                .Where(p => p.TaggedStoreId == storeId && p.Status == "ACTIVE" && p.Author.Role != "Banned")
                .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);

            var totalCount = await query.CountAsync();

            var posts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new SpotPostPagedResult
            {
                Items = posts.Select(p => new SpotPostSummaryDto
                {
                    Id = p.Id,
                    Content = p.Content.Length > 200 ? p.Content.Substring(0, 200) + "..." : p.Content,
                    LocationName = p.LocationName,
                    CreatedAt = p.CreatedAt,
                    Author = new SpotPostAuthorDto
                    {
                        Id = p.Author.Id,
                        FullName = p.Author.FullName,
                        AvatarUrl = p.Author.AvatarUrl
                    },
                    TaggedStore = p.TaggedStore == null ? null : new SpotPostTaggedStoreDto
                    {
                        Id = p.TaggedStore.Id,
                        Name = p.TaggedStore.Name,
                        Address = p.TaggedStore.Address,
                        Latitude = p.TaggedStore.Latitude,
                        Longitude = p.TaggedStore.Longitude
                    },
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    ThumbnailUrl = p.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).FirstOrDefault()?.ImageUrl,
                    TotalImages = p.Images.Count
                }).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // -------- Private helpers --------

        private static SpotPostDetailDto MapToDetailDto(SpotSharingPost post)
        {
            return new SpotPostDetailDto
            {
                Id = post.Id,
                Content = post.Content,
                LocationName = post.LocationName,
                Status = post.Status,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                Author = new SpotPostAuthorDto
                {
                    Id = post.Author.Id,
                    FullName = post.Author.FullName,
                    AvatarUrl = post.Author.AvatarUrl
                },
                TaggedStore = post.TaggedStore == null ? null : new SpotPostTaggedStoreDto
                {
                    Id = post.TaggedStore.Id,
                    Name = post.TaggedStore.Name,
                    Address = post.TaggedStore.Address,
                    Latitude = post.TaggedStore.Latitude,
                    Longitude = post.TaggedStore.Longitude
                },
                Latitude = post.Latitude,
                Longitude = post.Longitude,
                Images = post.Images.Select(img => new SpotImageDto
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    DisplayOrder = img.DisplayOrder
                }).ToList()
            };
        }

        private async Task RollbackUploadedImagesAsync(List<ImageUploadResult> uploadedImages)
        {
            if (uploadedImages.Count == 0) return;

            _logger.LogWarning("Rolling back {Count} uploaded spot image(s) from Cloudinary", uploadedImages.Count);
            foreach (var img in uploadedImages)
            {
                if (string.IsNullOrWhiteSpace(img.PublicId)) continue;
                try
                {
                    await _cloudinary.DeleteImageAsync(img.PublicId);
                    _logger.LogInformation("Rolled back Cloudinary spot image: {PublicId}", img.PublicId);
                }
                catch (Exception ex)
                {
                    // Log nhung khong throw - rollback best-effort
                    _logger.LogError(ex, "Failed to rollback Cloudinary spot image {PublicId}", img.PublicId);
                }
            }
        }
    }

}
