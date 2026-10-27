using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Doosii.BLL.Services;

public static class CommunityPostRules
{
    public static readonly IReadOnlySet<string> StyleTags = new HashSet<string>(
        "y2k 90s-vintage 80s-retro 70s-boho-disco classic-vintage nostalgia retro-sportswear old-school mid-century-modern 2010s-tumblr acubi blokecore gorpcore coquette fairycore cottagecore cyberpunk-techwear balletcore old-money e-girl-e-boy soft-girl-soft-boy indie-sleaze skater motocore-biker streetwear minimalist basic workwear office-core unisex menswear bohemian preppy grunge gothic-darkwear band-graphic-tee reworked-upcycled deadstock local-brand-pass high-end-thrift denim-on-denim oversized-boxy cropped parachute corduroy leather flannel-plaid knitwear crochet faux-fur".Split(' '), StringComparer.Ordinal);

    public static void Validate(CommunityPostWriteRequest r, IReadOnlyList<IFormFile> files, bool creating)
    {
        r.Type = (r.Type ?? "").Trim().ToUpperInvariant();
        r.Title = (r.Title ?? "").Trim(); r.PostDescription = (r.PostDescription ?? "").Trim();
        if (r.Type is not ("PASS" or "OTHER")) Fail("Loại bài phải là PASS hoặc OTHER.", "INVALID_POST_TYPE");
        if (!Text(r.Title, 150) || !Text(r.PostDescription, 5000)) Fail("Tiêu đề hoặc nội dung không hợp lệ.", "INVALID_POST_CONTENT");
        if (creating && r.ClientRequestId == Guid.Empty) Fail("Cần mã gửi bài hợp lệ.", "INVALID_CLIENT_REQUEST_ID");
        if (r.Type == "OTHER" && r.PassInfo != null) Fail("OTHER không có thông tin PASS.", "PASS_INFO_NOT_ALLOWED");
        if (r.Type == "PASS")
        {
            var p = r.PassInfo ?? throw new ServiceException(400, "Cần thông tin sản phẩm PASS.", "PASS_INFO_REQUIRED");
            p.ProductName = (p.ProductName ?? "").Trim(); p.ProductDescription = p.ProductDescription?.Trim();
            p.Size = (p.Size ?? "").Trim(); p.Defects = (p.Defects ?? "").Trim();
            if (!Text(p.ProductName, 300) || !Text(p.Size, 50) || !Text(p.Defects, 2000) || p.ProductDescription?.Length > 2000 ||
                p.Price <= 0 || p.Price >= 10000000000000000m || decimal.Round(p.Price, 2) != p.Price || p.ConditionPercent is < 0 or > 100)
                Fail("Thông tin sản phẩm PASS không hợp lệ.", "INVALID_PASS_INFO");
            if (p.StyleTagIds == null || p.StyleTagIds.Count is < 1 or > 50 || p.StyleTagIds.Any(t => t == null || !StyleTags.Contains(t)) || p.StyleTagIds.Distinct().Count() != p.StyleTagIds.Count)
                Fail("Chọn ít nhất một phong cách hợp lệ, không trùng.", "INVALID_STYLE_TAGS");
            p.StyleTagIds = p.StyleTagIds!.OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (p.Measurements?.Count > 30) Fail("Tối đa 30 số đo.", "INVALID_MEASUREMENTS");
            foreach (var m in p.Measurements ?? new())
            {
                if (m == null) Fail("Số đo không hợp lệ.", "INVALID_MEASUREMENTS");
                m!.Name = (m.Name ?? "").Trim(); m.Unit = (m.Unit ?? "").Trim();
                if (!Text(m.Name, 80) || !Text(m.Unit, 20) || !double.IsFinite(m.Value) || m.Value <= 0 || m.Value > 1000000)
                    Fail("Số đo không hợp lệ.", "INVALID_MEASUREMENTS");
            }
        }
        if (r.Media == null || r.Media.Count > 10 || r.Media.Any(m => m == null)) Fail("Tối đa 10 media mỗi bài.", "INVALID_MEDIA");
        var keys = new HashSet<string>(StringComparer.Ordinal); var ids = new HashSet<int>(); var positions = new HashSet<int>();
        foreach (var m in r.Media!)
        {
            m.MediaType = (m.MediaType ?? "").Trim().ToUpperInvariant(); m.Caption = m.Caption?.Trim();
            m.UploadKey = m.UploadKey?.Trim();
            if (m.MediaType is not ("IMAGE" or "VIDEO") || m.Caption?.Length > 1000 || m.DisplayOrder is < 0 or > 9 || !positions.Add(m.DisplayOrder)) Fail("Media hoặc thứ tự không hợp lệ.", "INVALID_MEDIA");
            if (m.StoreId <= 0 || m.ProductId <= 0) Fail("ID liên kết không hợp lệ.", "INVALID_MEDIA");
            if (r.Type == "PASS" && (m.StoreId.HasValue || m.ProductId.HasValue)) Fail("PASS không được gắn shop hoặc sản phẩm.", "PASS_TAG_NOT_ALLOWED");
            if (m.Id.HasValue)
            {
                if (creating || m.Id <= 0 || !string.IsNullOrEmpty(m.UploadKey) || !ids.Add(m.Id.Value)) Fail("ID media không hợp lệ.", "INVALID_MEDIA");
            }
            else if (!Text(m.UploadKey, 80) || !keys.Add(m.UploadKey!)) Fail("Mỗi media mới cần uploadKey duy nhất.", "INVALID_MEDIA");
        }
        if (r.Media.Count(m => m.MediaType == "VIDEO") > 2) Fail("Tối đa 2 video mỗi bài.", "INVALID_MEDIA");
        if (r.Type == "PASS" && !r.Media.Any(m => m.MediaType == "IMAGE")) Fail("PASS cần ít nhất một ảnh sản phẩm.", "INVALID_MEDIA");
        if (files.Count != keys.Count || files.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != files.Count || files.Any(f => !keys.Contains(f.Name))) Fail("File và uploadKey không khớp.", "INVALID_MEDIA");
        if (files.Sum(f => f.Length) > 140L * 1024 * 1024) throw new ServiceException(413, "Tổng file quá lớn.", "MEDIA_TOO_LARGE");
        r.Media = r.Media.OrderBy(m => m.DisplayOrder).ToList();
    }

    public static async Task ValidateFileAsync(IFormFile file, string type)
    {
        var max = (type == "VIDEO" ? 50L : 5L) * 1024 * 1024;
        if (file.Length <= 0) Fail("File rỗng.", "INVALID_MEDIA");
        if (file.Length > max) throw new ServiceException(413, "File vượt dung lượng cho phép.", "MEDIA_TOO_LARGE");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mime = file.ContentType.ToLowerInvariant();
        await using var stream = file.OpenReadStream();
        var header = new byte[16]; var count = await stream.ReadAtLeastAsync(header, 12, false);
        bool valid = count >= 12 && (type == "VIDEO"
            ? ext == ".mp4" && mime == "video/mp4" && header.AsSpan(4, 4).SequenceEqual("ftyp"u8)
            : (ext is ".jpg" or ".jpeg") && mime is "image/jpeg" or "image/jpg" && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff
              || ext == ".png" && mime == "image/png" && header.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
              || ext == ".webp" && mime == "image/webp" && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8));
        if (!valid) Fail("Nội dung file không khớp định dạng ảnh/video cho phép.", "INVALID_MEDIA");
    }
    internal static async Task<string> FingerprintAsync(CommunityPostWriteRequest r, IReadOnlyList<IFormFile> files)
    {
        var hashes = new List<object>();
        foreach (var f in files.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            await using var stream = f.OpenReadStream();
            hashes.Add(new { f.Name, Hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)) });
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Request = r, Files = hashes })));
    }
    private static bool Text(string? text, int max) => !string.IsNullOrWhiteSpace(text) && text.Length <= max;
    private static void Fail(string message, string code) => throw new ServiceException(400, message, code);
}
