using Microsoft.AspNetCore.Http;
using Doosii.BLL.Exceptions;

namespace Doosii.BLL.Services;
internal static class CommunityPostingRules
{
    public static void ValidateImages(List<IFormFile> files)
    {
        if (files == null || files.Count is < 1 or > 5)
            throw new ServiceException(400, "Bai viet can 1 den 5 anh.", "INVALID_IMAGE_COUNT");
        var types = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        foreach (var file in files)
            if (file == null || file.Length is < 1 or > 5242880 || !types.Contains(file.ContentType.ToLowerInvariant()))
                throw new ServiceException(400, "Moi anh JPG/PNG/WebP phai lon hon 0 va khong qua 5 MB.", "INVALID_IMAGE_FILE");
    }
}
