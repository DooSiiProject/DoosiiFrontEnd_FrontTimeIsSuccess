using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace Doosii.BLL.Services
{
    using DoosiImageUploadResult = Doosii.BLL.DTOs.ImageUploadResult;

    /// <summary>
    /// Service xu ly upload va quan ly anh tren Cloudinary.
    /// An toan, co validation dinh dang, dung luong, whitelist folder va rollback khi loi.
    /// </summary>
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary? _cloudinary;
        private readonly ILogger<CloudinaryService> _logger;
        private readonly string _rootFolder;

        // Dinh dang anh duoc phep
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

        private static readonly HashSet<string> AllowedMimeTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/jpg", "image/png", "image/webp"
            };

        // Whitelist folder hop le de tranh upload lung tung
        private static readonly HashSet<string> AllowedSubFolders =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "products", "stores", "kyc", "reviews", "avatars", "forum", ""
            };

        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        public bool IsConfigured => _cloudinary != null;

        public CloudinaryService(IConfiguration configuration, ILogger<CloudinaryService> logger)
        {
            _logger = logger;
            _rootFolder = configuration["Cloudinary:Folder"] ?? "doosii";

            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            // Neu tat ca 3 gia tri deu co, khoi tao Cloudinary client
            if (!string.IsNullOrWhiteSpace(cloudName) &&
                !string.IsNullOrWhiteSpace(apiKey) &&
                !string.IsNullOrWhiteSpace(apiSecret))
            {
                var account = new Account(cloudName, apiKey, apiSecret);
                _cloudinary = new Cloudinary(account);
                _cloudinary.Api.Secure = true;
                _logger.LogInformation("Cloudinary configured with CloudName: {CloudName}", cloudName);
            }
            else
            {
                _logger.LogWarning("Cloudinary credentials not configured. Upload will be unavailable.");
            }
        }

        public async Task<DoosiImageUploadResult> UploadImageAsync(IFormFile file, string subFolder = "")
        {
            ValidateCloudinaryConfigured();
            ValidateSubFolder(subFolder);
            ValidateFile(file);

            var folder = string.IsNullOrWhiteSpace(subFolder)
                ? _rootFolder
                : $"{_rootFolder}/{subFolder}";

            var publicId = $"{Guid.NewGuid():N}";

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, file.OpenReadStream()),
                Folder = folder,
                PublicId = publicId,
                Overwrite = false,
                UseFilename = false,
                UniqueFilename = true
            };

            _logger.LogInformation("Uploading image to Cloudinary folder: {Folder}", folder);

            CloudinaryDotNet.Actions.ImageUploadResult cloudResult;
            try
            {
                cloudResult = await _cloudinary!.UploadAsync(uploadParams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloudinary upload failed for file {FileName}", file.FileName);
                throw new ServiceException(502, "Image upload failed. Please try again later.", "UPLOAD_FAILED");
            }

            if (cloudResult.Error != null)
            {
                _logger.LogError("Cloudinary returned error: {Error}", cloudResult.Error.Message);
                throw new ServiceException(502, "Image upload failed: " + cloudResult.Error.Message, "UPLOAD_FAILED");
            }

            return new DoosiImageUploadResult
            {
                Url = cloudResult.Url?.ToString() ?? string.Empty,
                SecureUrl = cloudResult.SecureUrl?.ToString() ?? string.Empty,
                PublicId = cloudResult.PublicId,
                Width = cloudResult.Width,
                Height = cloudResult.Height,
                Format = cloudResult.Format,
                Bytes = cloudResult.Bytes,
                CreatedAt = cloudResult.CreatedAt == default ? DateTime.UtcNow : cloudResult.CreatedAt
            };
        }

        public async Task<List<DoosiImageUploadResult>> UploadImagesAsync(IList<IFormFile> files, string subFolder = "")
        {
            ValidateCloudinaryConfigured();
            ValidateSubFolder(subFolder);

            if (files == null || files.Count == 0)
            {
                throw new ServiceException(400, "No files provided.", "NO_FILES");
            }

            if (files.Count > 5)
            {
                throw new ServiceException(400, "Maximum 5 files allowed per request.", "TOO_MANY_FILES");
            }

            // Validate tat ca file truoc khi bat dau upload
            foreach (var file in files)
            {
                ValidateFile(file);
            }

            var results = new List<DoosiImageUploadResult>();
            var uploaded = new List<string>(); // Theo doi publicId da upload de rollback neu can

            foreach (var file in files)
            {
                try
                {
                    var result = await UploadImageAsync(file, subFolder);
                    results.Add(result);
                    uploaded.Add(result.PublicId);
                }
                catch (ServiceException)
                {
                    // Rollback cac anh da upload thanh cong truoc do
                    _logger.LogWarning("Upload failed for one file; rolling back {Count} already-uploaded images.", uploaded.Count);
                    foreach (var pid in uploaded)
                    {
                        try { await DeleteImageAsync(pid); }
                        catch (Exception ex) { _logger.LogError(ex, "Failed to rollback image {PublicId}", pid); }
                    }
                    throw;
                }
            }

            return results;
        }

        public async Task DeleteImageAsync(string publicId)
        {
            ValidateCloudinaryConfigured();

            if (string.IsNullOrWhiteSpace(publicId))
            {
                throw new ServiceException(400, "PublicId is required.", "INVALID_PUBLIC_ID");
            }

            var deleteParams = new DeletionParams(publicId);

            try
            {
                var result = await _cloudinary!.DestroyAsync(deleteParams);
                if (result.Error != null)
                {
                    _logger.LogError("Cloudinary delete error for {PublicId}: {Error}", publicId, result.Error.Message);
                    throw new ServiceException(502, "Cloudinary delete error: " + result.Error.Message, "DELETE_FAILED");
                }
                else
                {
                    _logger.LogInformation("Deleted Cloudinary image: {PublicId}", publicId);
                }
            }
            catch (ServiceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting Cloudinary image {PublicId}", publicId);
                throw new ServiceException(502, "Image deletion failed.", "DELETE_FAILED");
            }
        }

        // ----------- Private helpers -----------

        private void ValidateCloudinaryConfigured()
        {
            if (_cloudinary == null)
            {
                throw new ServiceException(503, "Cloudinary is not configured on this server.", "CLOUDINARY_NOT_CONFIGURED");
            }
        }

        private static void ValidateSubFolder(string subFolder)
        {
            if (!AllowedSubFolders.Contains(subFolder))
            {
                throw new ServiceException(400, $"Folder ''{subFolder}'' is not allowed.", "INVALID_UPLOAD_FOLDER");
            }
        }

        private static void ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ServiceException(400, "File is empty or missing.", "EMPTY_FILE");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                throw new ServiceException(400, $"File ''{file.FileName}'' exceeds the 5 MB size limit.", "FILE_TOO_LARGE");
            }

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext))
            {
                throw new ServiceException(400, $"File extension ''{ext}'' is not supported. Allowed: .jpg, .jpeg, .png, .webp", "INVALID_IMAGE_FORMAT");
            }

            var mime = file.ContentType?.ToLowerInvariant() ?? string.Empty;
            if (!AllowedMimeTypes.Contains(mime))
            {
                throw new ServiceException(400, $"MIME type ''{file.ContentType}'' is not supported.", "INVALID_IMAGE_FORMAT");
            }
        }
    }
}
