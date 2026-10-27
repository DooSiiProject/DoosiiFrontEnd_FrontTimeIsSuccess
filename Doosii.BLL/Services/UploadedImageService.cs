using Microsoft.EntityFrameworkCore.Metadata;
using System.Data;
using System.Text.RegularExpressions;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Doosii.BLL.Services;

public class UploadedImageService(AppDbContext db, ICloudinaryService cloud, IConfiguration configuration, ILogger<UploadedImageService> logger)
{
    public async Task RegisterAsync(int ownerId, IEnumerable<ImageUploadResult> results)
    {
        var list = results.ToList();
        try {
            db.UploadedImages.AddRange(list.Select(i => new UploadedImage { OwnerId = ownerId, PublicId = i.PublicId, Url = string.IsNullOrEmpty(i.SecureUrl) ? i.Url : i.SecureUrl }));
            await db.SaveChangesAsync();
        } catch {
            foreach (var image in list) {
                try { await cloud.DeleteImageAsync(image.PublicId); }
                catch (Exception ex) { logger.LogError(ex, "Could not roll back unregistered upload {PublicId}", image.PublicId); }
            }
            throw;
        }
    }

    // Import only canonical URLs that are currently attached to this owner's
    // editable product or merchant profile. Never derive deletion authority from a URL alone.
    public async Task<List<ImageUploadResult>> ResolveAsync(int ownerId, List<string> urls)
    {
        if (urls.Count > 100) throw new ServiceException(400, "Tối đa 100 ảnh mỗi lượt kiểm tra.", "TOO_MANY_IMAGES");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var results = new List<ImageUploadResult>();
        foreach (var url in urls.Distinct(StringComparer.Ordinal)) {
            var publicId = ParsePublicId(url);
            if (publicId == null) continue;
            var image = await db.UploadedImages.SingleOrDefaultAsync(i => i.PublicId == publicId);
            if (image != null) {
                if (image.OwnerId == ownerId && image.State == "ACTIVE") results.Add(new() { PublicId = image.PublicId, SecureUrl = image.Url, Url = url });
                continue;
            }
            var owned = await db.ProductImages.AnyAsync(i => i.ImageUrl == url && i.Product.Store.OwnerId == ownerId) ||
                await db.Products.AnyAsync(p => p.Store.OwnerId == ownerId && p.DetailsJson != null && p.DetailsJson.Contains(publicId)) ||
                await db.ProductGroupImages.AnyAsync(i => i.ImageUrl == url && i.ProductGroup.Store.OwnerId == ownerId) ||
                await db.MerchantProfiles.AnyAsync(p => p.UserId == ownerId &&
                    (p.ShopMediaUrls != null && p.ShopMediaUrls.Contains(publicId) || p.IdCardFrontUrl == url || p.IdCardBackUrl == url || p.LicenseImageUrl == url || p.FrontFacadeUrl == url));
            if (!owned) continue;
            image = new() { OwnerId = ownerId, PublicId = publicId, Url = url };
            db.UploadedImages.Add(image);
            await db.SaveChangesAsync();
            results.Add(new() { PublicId = publicId, Url = url, SecureUrl = url });
        }
        await tx.CommitAsync();
        return results;
    }

    private string? ParsePublicId(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Host != "res.cloudinary.com" || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.Scheme is not ("https" or "http")) return null;
        var prefix = "/" + configuration["Cloudinary:CloudName"] + "/image/upload/";
        if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var path = uri.AbsolutePath[prefix.Length..];
        path = Regex.Replace(path, @"^v\d+/", "");
        var root = (configuration["Cloudinary:Folder"] ?? "doosii").Trim('/') + "/";
        if (!path.StartsWith(root, StringComparison.Ordinal) || !Regex.IsMatch(path, @"^[a-zA-Z0-9_/-]+\.(jpg|jpeg|png|webp)$")) return null;
        return path[..path.LastIndexOf('.')];
    }

    public async Task DeleteAsync(int ownerId, string publicId)
    {
        // A transaction marks the image unavailable before the external request.
        // A failed or interrupted cloud deletion stays retryable in DELETING state.
        await using (var tx = await db.Database.BeginTransactionAsync()) {
            var image = await db.UploadedImages.FromSqlInterpolated($"SELECT * FROM [media].[UploadedImages] WITH (UPDLOCK,HOLDLOCK) WHERE [PublicId] = {publicId}").AsNoTracking().SingleOrDefaultAsync();
            if (image == null || image.OwnerId != ownerId) throw new ServiceException(404, "Không tìm thấy ảnh thuộc tài khoản của bạn.", "IMAGE_NOT_FOUND");
            if (image.State == "DELETED") { await tx.CommitAsync(); return; }
            if (await IsReferencedAsync(image)) throw new ServiceException(409, "Ảnh vẫn đang được sử dụng; đã giữ lại ảnh trên máy chủ.", "IMAGE_IN_USE");
            await db.UploadedImages.Where(i => i.Id == image.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.State, "DELETING"));
            await tx.CommitAsync();
        }
        await cloud.DeleteImageAsync(publicId);
        // ExecuteUpdate is intentional: the row is already a deletion tombstone.
        await db.UploadedImages.Where(i => i.PublicId == publicId && i.OwnerId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.State, "DELETED").SetProperty(i => i.DeletedAt, DateTime.UtcNow));
    }

    private async Task<bool> IsReferencedAsync(UploadedImage image)
    {
        // Include every mapped string column (also JSON and immutable order/chat
        // snapshots). False positives retain an image rather than breaking a reference.
        var connection = db.Database.GetDbConnection();
        foreach (var entity in db.Model.GetEntityTypes().Where(e => e.ClrType != typeof(UploadedImage))) {
            var table = entity.GetTableName();
            if (table == null) continue;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var columns = entity.GetProperties().Where(p => p.ClrType == typeof(string)).Select(p => p.GetColumnName(store)).Where(c => c != null).Distinct().ToArray();
            if (columns.Length == 0) continue;
            static string Q(string name) => "[" + name.Replace("]", "]]") + "]";
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = $"SELECT TOP (1) 1 FROM {Q(entity.GetSchema() ?? "dbo")}.{Q(table)} WHERE " + string.Join(" OR ", columns.Select(c => $"CHARINDEX(@pid, {Q(c!)}) > 0 OR CHARINDEX(@escaped, {Q(c!)}) > 0"));
            var parameter = command.CreateParameter(); parameter.ParameterName = "@pid"; parameter.Value = image.PublicId; command.Parameters.Add(parameter);
            var escaped = command.CreateParameter(); escaped.ParameterName = "@escaped"; escaped.Value = image.PublicId.Replace("/", "\\/"); command.Parameters.Add(escaped);
            if (await command.ExecuteScalarAsync() != null) return true;
        }
        return false;
    }
}
