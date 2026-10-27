using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Doosii.BLL.Services;

public sealed class CommunityMediaStorage : ICommunityMediaStorage
{
    private readonly Cloudinary? _cloud;
    public CommunityMediaStorage(IConfiguration configuration)
    {
        var name = configuration["Cloudinary:CloudName"]; var key = configuration["Cloudinary:ApiKey"]; var secret = configuration["Cloudinary:ApiSecret"];
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(secret))
        { _cloud = new Cloudinary(new Account(name, key, secret)); _cloud.Api.Secure = true; }
    }
    public async Task<CommunityUploadedMedia> UploadAsync(IFormFile file, string mediaType, string publicId)
    {
        var cloud = _cloud ?? throw new ServiceException(503, "Chưa cấu hình lưu trữ media.", "CLOUDINARY_NOT_CONFIGURED");
        await CommunityPostRules.ValidateFileAsync(file, mediaType);
        await using var stream = file.OpenReadStream();
        try
        {
            if (mediaType == "IMAGE")
            {
                var r = await cloud.UploadAsync(new ImageUploadParams { File = new FileDescription(file.FileName, stream), PublicId = publicId, Overwrite = false });
                if (r.Error != null || r.Width <= 0 || r.Height <= 0 || r.Format is not ("jpg" or "jpeg" or "png" or "webp") || r.SecureUrl == null)
                    throw new ServiceException(400, "Không giải mã được ảnh hợp lệ.", "INVALID_MEDIA");
                return new(publicId, mediaType, r.SecureUrl.ToString(), null);
            }
            var v = await cloud.UploadAsync(new VideoUploadParams { File = new FileDescription(file.FileName, stream), PublicId = publicId, Overwrite = false });
            if (v.Error != null || v.SecureUrl == null || v.IsAudio || v.Width <= 0 || v.Height <= 0 || !double.IsFinite(v.Duration) || v.Duration <= 0 || v.Duration > 60 || v.Format != "mp4" || v.Video?.Codec != "h264" || (v.Audio != null && v.Audio.Codec != "aac"))
                throw new ServiceException(400, "Video cần MP4 H.264, âm thanh AAC nếu có, tối đa 60 giây.", "INVALID_MEDIA");
            var url = v.SecureUrl.ToString();
            var thumbnail = url[..url.LastIndexOf('.')] + ".jpg";
            thumbnail = thumbnail.Replace("/video/upload/", "/video/upload/so_0/");
            return new(publicId, mediaType, url, thumbnail);
        }
        catch (ServiceException) { throw; }
        catch (Exception) { throw new ServiceException(502, "Không tải được media. Hãy thử lại.", "UPLOAD_FAILED"); }
    }
    public async Task DeleteAsync(string publicId, string mediaType)
    {
        var cloud = _cloud ?? throw new ServiceException(503, "Chưa cấu hình lưu trữ media.", "CLOUDINARY_NOT_CONFIGURED");
        var r = await cloud.DestroyAsync(new DeletionParams(publicId) { ResourceType = mediaType == "VIDEO" ? ResourceType.Video : ResourceType.Image, Invalidate = true });
        if (r.Error != null || r.Result is not ("ok" or "not found")) throw new ServiceException(502, "Chưa xóa được media.", "MEDIA_DELETE_FAILED");
    }
}
