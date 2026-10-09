using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;

namespace Doosii.API.Controllers
{
    /// <summary>
    /// Upload va quan ly anh tren Cloudinary. Yeu cau dang nhap.
    /// </summary>
    [ApiController]
    [Route("api/uploads")]
    [Authorize]
    public class UploadsController : ControllerBase
    {
        private readonly ICloudinaryService _cloudinaryService;
        private readonly ILogger<UploadsController> _logger;

        // Whitelist folder hop le cho frontend truyen len
        private static readonly HashSet<string> AllowedFolders =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "products", "stores", "kyc", "reviews", "avatars", "forum", ""
            };

        public UploadsController(ICloudinaryService cloudinaryService, ILogger<UploadsController> logger)
        {
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

        /// <summary>
        /// Upload 1 anh don len Cloudinary.
        /// Content-Type: multipart/form-data
        /// Cac dinh dang duoc phep: jpg, jpeg, png, webp.
        /// Dung luong toi da: 5 MB.
        /// </summary>
        /// <param name="file">File anh (field name: file).</param>
        /// <param name="folder">Thu muc con: products | stores | kyc | reviews | avatars | forum (mac dinh: products).</param>
        [HttpPost("image")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage(
            [FromForm] IFormFile file,
            [FromQuery] string folder = "products")
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le.", new List<string> { "UNAUTHORIZED" }));
            }

            if (!AllowedFolders.Contains(folder))
            {
                return BadRequest(ApiResponse.Fail(
                    $"Folder '{folder}' khong duoc phep.",
                    new List<string> { "INVALID_UPLOAD_FOLDER" }));
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse.Fail(
                    "Khong co file nao duoc gui len.",
                    new List<string> { "NO_FILES" }));
            }

            if (!_cloudinaryService.IsConfigured)
            {
                _logger.LogWarning("Upload attempted but Cloudinary is not configured. User: {UserId}", userId);
                return StatusCode(503, ApiResponse.Fail(
                    "Dich vu upload anh chua duoc cau hinh tren server nay.",
                    new List<string> { "CLOUDINARY_NOT_CONFIGURED" }));
            }

            try
            {
                _logger.LogInformation("User {UserId} uploading single image to folder '{Folder}'", userId, folder);
                var result = await _cloudinaryService.UploadImageAsync(file, folder);
                return StatusCode(201, ApiResponse.Ok(result, "Image uploaded successfully"));
            }
            catch (ServiceException ex)
            {
                _logger.LogWarning("Upload failed for user {UserId}: [{ErrorCode}] {Message}", userId, ex.ErrorCode, ex.Message);
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during single image upload for user {UserId}", userId);
                return StatusCode(500, ApiResponse.Fail(
                    "An unexpected error occurred during upload.",
                    new List<string> { "INTERNAL_ERROR" }));
            }
        }

        /// <summary>
        /// Upload tu 1 den 5 anh len Cloudinary.
        /// Content-Type: multipart/form-data
        /// Cac dinh dang duoc phep: jpg, jpeg, png, webp.
        /// Dung luong toi da moi file: 5 MB.
        /// </summary>
        /// <param name="files">Danh sach file (field name: files).</param>
        /// <param name="folder">Thu muc con: products | stores | kyc | reviews | avatars | forum (mac dinh: products).</param>
        [HttpPost("images")]
        [RequestSizeLimit(30 * 1024 * 1024)] // 30 MB tong (5 file x 5MB + overhead)
        [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
        public async Task<IActionResult> UploadImages(
            [FromForm] List<IFormFile> files,
            [FromQuery] string folder = "products")
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le.", new List<string> { "UNAUTHORIZED" }));
            }

            // 1. Validate folder whitelist
            if (!AllowedFolders.Contains(folder))
            {
                return BadRequest(ApiResponse.Fail(
                    $"Folder '{folder}' khong duoc phep.",
                    new List<string> { "INVALID_UPLOAD_FOLDER" }));
            }

            // 2. Validate so luong file
            if (files == null || files.Count == 0)
            {
                return BadRequest(ApiResponse.Fail(
                    "Khong co file nao duoc gui len.",
                    new List<string> { "NO_FILES" }));
            }

            if (files.Count > 5)
            {
                return BadRequest(ApiResponse.Fail(
                    "Chi cho phep toi da 5 file trong mot request.",
                    new List<string> { "TOO_MANY_FILES" }));
            }

            // 3. Kiem tra Cloudinary da cau hinh chua (fail-fast truoc khi doc file)
            if (!_cloudinaryService.IsConfigured)
            {
                _logger.LogWarning("Upload attempted but Cloudinary is not configured. User: {UserId}", userId);
                return StatusCode(503, ApiResponse.Fail(
                    "Dich vu upload anh chua duoc cau hinh tren server nay.",
                    new List<string> { "CLOUDINARY_NOT_CONFIGURED" }));
            }

            try
            {
                _logger.LogInformation("User {UserId} uploading {Count} file(s) to folder '{Folder}'",
                    userId, files.Count, folder);

                var results = await _cloudinaryService.UploadImagesAsync(files, folder);

                _logger.LogInformation("User {UserId} uploaded {Count} image(s) successfully.", userId, results.Count);

                var message = results.Count == 1
                    ? "Image uploaded successfully"
                    : $"{results.Count} images uploaded successfully";

                return StatusCode(201, ApiResponse.Ok(results, message));
            }
            catch (ServiceException ex)
            {
                _logger.LogWarning("Upload failed for user {UserId}: [{ErrorCode}] {Message}", userId, ex.ErrorCode, ex.Message);
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during image upload for user {UserId}", userId);
                return StatusCode(500, ApiResponse.Fail(
                    "An unexpected error occurred during upload.",
                    new List<string> { "INTERNAL_ERROR" }));
            }
        }

        /// <summary>
        /// Xoa anh tren Cloudinary bang publicId.
        /// </summary>
        /// <param name="publicId">PublicId cua anh (co the chua duong dan, vd: doosii/products/abc123xyz).</param>
        [HttpDelete("{*publicId}")]
        public async Task<IActionResult> DeleteImage([FromRoute] string publicId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse.Fail("Token khong hop le.", new List<string> { "UNAUTHORIZED" }));
            }

            if (string.IsNullOrWhiteSpace(publicId))
            {
                return BadRequest(ApiResponse.Fail("PublicId khong duoc de trong.", new List<string> { "INVALID_PUBLIC_ID" }));
            }

            if (!_cloudinaryService.IsConfigured)
            {
                return StatusCode(503, ApiResponse.Fail(
                    "Dich vu upload anh chua duoc cau hinh tren server nay.",
                    new List<string> { "CLOUDINARY_NOT_CONFIGURED" }));
            }

            try
            {
                _logger.LogInformation("User {UserId} deleting image with publicId: {PublicId}", userId, publicId);
                await _cloudinaryService.DeleteImageAsync(publicId);
                return Ok(ApiResponse.Ok(new { }, "Image deleted successfully"));
            }
            catch (ServiceException ex)
            {
                return StatusCode(ex.StatusCode, ApiResponse.Fail(ex.Message, new List<string> { ex.ErrorCode }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting image {PublicId}", publicId);
                return StatusCode(500, ApiResponse.Fail("An unexpected error occurred.", new List<string> { "INTERNAL_ERROR" }));
            }
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int id)) return null;
            return id;
        }
    }
}

