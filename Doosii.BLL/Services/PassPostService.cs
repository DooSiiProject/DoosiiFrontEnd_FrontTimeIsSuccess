using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;

namespace Doosii.BLL.Services
{
    public class PassPostService : IPassPostService
    {
        private const int MaxImagesPerPost = 5;
        private const int MinImagesPerPost = 1;
        private const int MaxDailyPosts = 10;
        private const string UploadFolder = "forum";
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private readonly AppDbContext _db;
        private readonly ICloudinaryService _cloudinary;
        private readonly ILogger<PassPostService> _logger;

        public PassPostService(
            AppDbContext db,
            ICloudinaryService cloudinary,
            ILogger<PassPostService> logger)
        {
            _db = db;
            _cloudinary = cloudinary;
            _logger = logger;
        }

        /// <summary>
        /// Tao moi bai PassPost:
        /// 1. Validate request (title, price, condition, image count va image format).
        /// 2. Kiem tra gioi han 10 bai/24 gio.
        /// 3. Kiem tra Cloudinary da cau hinh chua.
        /// 4. Upload anh vao Cloudinary folder 'forum' - neu 1 anh loi thi rollback cac anh da upload.
        /// 5. Ghi database trong transaction - neu loi thi xoa het anh Cloudinary.
        /// </summary>
        public async Task<CreatePassPostResponse> CreatePassPostAsync(int authorId, CreatePassPostRequest request)
        {
            // ---- 0. Verify Author exists ----
            var author = await _db.Users.FindAsync(authorId);
            if (author == null)
            {
                throw new ServiceException(401, "Tai khoan khong hop le.", "UNAUTHORIZED");
            }

            if (author.Role == "Banned") throw new ServiceException(403, "Tai khoan da bi khoa.", "USER_BANNED");
            CommunityPostingRules.ValidateImages(request.Images);

            // ---- 1. Input Validation ----
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 150)
                throw new ServiceException(400, "Title la bat buoc va toi da 150 ky tu.", "INVALID_TITLE");

            if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 5000)
                throw new ServiceException(400, "Description la bat buoc.", "INVALID_DESCRIPTION");

            if (request.Price <= 0 || decimal.Round(request.Price, 2) != request.Price || request.Price > 9999999999999999.99m)
                throw new ServiceException(400, "Price phai lon hon 0.", "INVALID_PRICE");

            if (string.IsNullOrWhiteSpace(request.Size) || request.Size.Length > 50)
                throw new ServiceException(400, "Size la bat buoc.", "INVALID_SIZE");

            if (request.ConditionPercent < 0 || request.ConditionPercent > 100)
                throw new ServiceException(400, "ConditionPercent phai tu 0 den 100.", "INVALID_CONDITION");

            if (request.Images == null || request.Images.Count < MinImagesPerPost)
                throw new ServiceException(400, $"Bai dang can it nhat {MinImagesPerPost} anh.", "INVALID_IMAGE_COUNT");

            if (request.Images.Count > MaxImagesPerPost)
                throw new ServiceException(400, $"Bai dang chi duoc toi da {MaxImagesPerPost} anh.", "INVALID_IMAGE_COUNT");

            // Validate tat ca file anh truoc khi bat ky upload nao dien ra
            foreach (var img in request.Images)
            {
                if (img == null || img.Length == 0)
                    throw new ServiceException(400, "File anh khong hop le hoac trong.", "EMPTY_FILE");

                var ext = Path.GetExtension(img.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                    throw new ServiceException(400, $"Dinh dang file '{ext}' khong duoc ho tro. Chi chap nhan .jpg, .jpeg, .png, .webp.", "INVALID_IMAGE_FORMAT");
            }

            if (request.StyleTags?.Length > 500) throw new ServiceException(400, "Phong cach toi da 500 ky tu.", "INVALID_STYLE");

            // ---- 2. Daily Limit Check (Max 10 bai trong 24h) ----
            var since = DateTime.UtcNow.AddHours(-24);
            var postsLast24h = await _db.PassPosts
                .CountAsync(p => p.AuthorId == authorId && p.CreatedAt >= since);

            if (postsLast24h >= MaxDailyPosts)
            {
                _logger.LogWarning("User {AuthorId} exceeded daily pass post limit ({Count}/{Max})",
                    authorId, postsLast24h, MaxDailyPosts);
                throw new ServiceException(429, "Ban da dat gioi han 10 bai pass trong 24 gio.", "PASS_POST_DAILY_LIMIT_REACHED");
            }

            // ---- 3. Cloudinary Config Check ----
            if (!_cloudinary.IsConfigured)
            {
                _logger.LogWarning("Cloudinary not configured; cannot create PassPost for user {AuthorId}", authorId);
                throw new ServiceException(503, "Dich vu upload anh chua duoc cau hinh.", "CLOUDINARY_NOT_CONFIGURED");
            }

            // ---- 4. Upload Images (with rollback on partial failure) ----
            var uploadedImages = new List<ImageUploadResult>();
            try
            {
                for (int i = 0; i < request.Images.Count; i++)
                {
                    var file = request.Images[i];
                    _logger.LogInformation("Uploading image {Index}/{Total} for user {AuthorId}", i + 1, request.Images.Count, authorId);
                    var result = await _cloudinary.UploadImageAsync(file, UploadFolder);
                    uploadedImages.Add(result);
                }
            }
            catch (ServiceException)
            {
                // Rollback cac anh da upload thanh cong truoc do neu mot anh bi loi
                await RollbackUploadedImagesAsync(uploadedImages);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during image upload for user {AuthorId}", authorId);
                await RollbackUploadedImagesAsync(uploadedImages);
                throw new ServiceException(502, "Upload anh that bai, vui long thu lai.", "UPLOAD_FAILED");
            }

            // ---- 5. Database Transaction ----
            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var liveAuthor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == authorId);
                if (liveAuthor == null || liveAuthor.Role == "Banned") throw new ServiceException(403, "Tai khoan khong the dang bai.", "USER_BANNED");
                if (await _db.PassPosts.CountAsync(p => p.AuthorId == authorId && p.CreatedAt >= since) >= MaxDailyPosts)
                    throw new ServiceException(429, "Ban da dat gioi han 10 bai pass trong 24 gio.", "PASS_POST_DAILY_LIMIT_REACHED");
                var passPost = new PassPost
                {
                    AuthorId = authorId,
                    Title = request.Title.Trim(),
                    Description = request.Description.Trim(),
                    Price = request.Price,
                    Size = request.Size.Trim(),
                    ConditionPercent = request.ConditionPercent,
                    AllowEscrow = request.AllowEscrow,
                    Status = "ACTIVE",
                    StyleTags = request.StyleTags?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                _db.PassPosts.Add(passPost);
                await _db.SaveChangesAsync();

                // Tao PostImage records theo thu tu upload
                var postImages = uploadedImages
                    .Select((img, idx) => new PostImage
                    {
                        PostId = passPost.Id,
                        ImageUrl = img.SecureUrl,
                        PublicId = img.PublicId,
                        DisplayOrder = idx
                    })
                    .ToList();

                _db.PostImages.AddRange(postImages);
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("PassPost {PostId} created by user {AuthorId} with {ImageCount} image(s)",
                    passPost.Id, authorId, postImages.Count);

                return new CreatePassPostResponse
                {
                    Id = passPost.Id,
                    Title = passPost.Title,
                    Status = passPost.Status,
                    CreatedAt = passPost.CreatedAt,
                    Images = postImages.Select(img => new PassPostImageDto
                    {
                        Id = img.Id,
                        ImageUrl = img.ImageUrl,
                        DisplayOrder = img.DisplayOrder
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database error creating PassPost for user {AuthorId}, rolling back {Count} Cloudinary image(s)",
                    authorId, uploadedImages.Count);

                // Xoa anh tren Cloudinary vi database insert that bai
                await RollbackUploadedImagesAsync(uploadedImages);

                if (ex is ServiceException) throw;
                throw new ServiceException(500, "Tao bai dang that bai do loi he thong.", "INTERNAL_ERROR");
            }
        }

        /// <summary>
        /// Lay chi tiet bai PassPost theo ID.
        /// Chi tra ve bai co status != HIDDEN.
        /// </summary>
        public async Task<PassPostDetailDto?> GetPassPostByIdAsync(int postId)
        {
            var post = await _db.PassPosts
                .Include(p => p.Author)
                .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                .FirstOrDefaultAsync(p => p.Id == postId && (p.Status == "ACTIVE" || p.Status == "LOCKED" || p.Status == "SOLD") && p.Author.Role != "Banned");

            if (post == null) return null;

            return new PassPostDetailDto
            {
                Id = post.Id,
                Title = post.Title,
                Description = post.Description,
                Price = post.Price,
                Size = post.Size,
                ConditionPercent = post.ConditionPercent,
                AllowEscrow = post.AllowEscrow,
                Status = post.Status,
                StyleTags = post.StyleTags,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                Author = new PassPostAuthorDto
                {
                    Id = post.Author.Id,
                    FullName = post.Author.FullName,
                    AvatarUrl = post.Author.AvatarUrl
                },
                Images = post.Images.Select(img => new PassPostImageDto
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    DisplayOrder = img.DisplayOrder
                }).ToList()
            };
        }

        // -------- Private Helpers --------

        private async Task RollbackUploadedImagesAsync(List<ImageUploadResult> uploadedImages)
        {
            if (uploadedImages.Count == 0) return;

            _logger.LogWarning("Rolling back {Count} uploaded image(s) from Cloudinary", uploadedImages.Count);
            foreach (var img in uploadedImages)
            {
                if (string.IsNullOrWhiteSpace(img.PublicId)) continue;
                try
                {
                    await _cloudinary.DeleteImageAsync(img.PublicId);
                    _logger.LogInformation("Rolled back Cloudinary image: {PublicId}", img.PublicId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to rollback Cloudinary image {PublicId}", img.PublicId);
                }
            }
        }
    }
}
