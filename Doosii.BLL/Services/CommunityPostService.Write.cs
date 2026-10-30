using System.Data;
using System.Text.Json;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Helpers;
using Doosii.DAL.Models.Forum;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Doosii.BLL.Services;

public partial class CommunityPostService
{
    public async Task<CommunityPostDto> CreateAsync(int actorId, CommunityPostWriteRequest request, IReadOnlyList<IFormFile> files)
    {
        CommunityPostRules.Validate(request, files, true);
        await PostingActorAsync(actorId, request.Type);
        foreach (var file in files) await CommunityPostRules.ValidateFileAsync(file, request.Media.Single(m => m.UploadKey == file.Name).MediaType);
        var hash = await CommunityPostRules.FingerprintAsync(request, files);
        var existing = await db.Posts.AsNoTracking().SingleOrDefaultAsync(p => p.AuthorId == actorId && p.ClientRequestId == request.ClientRequestId);
        if (existing != null) { CheckRequest(existing, hash); return await DetailAsync(existing.Id, actorId); }
        await AttachmentsAsync(actorId, request);
        // Advisory check avoids unnecessary cloud uploads; the transaction repeats it under the author lock.
        if (request.Type == "PASS" && await db.Posts.CountAsync(p => p.AuthorId == actorId && p.Type == "PASS" && p.CreatedAt >= DateTime.UtcNow.AddHours(-24)) >= 10)
            throw new ServiceException(429, "Tối đa 10 bài PASS trong 24 giờ.", "PASS_DAILY_LIMIT");
        var uploaded = new Dictionary<string, CommunityUploadedMedia>();
        bool attached = false;
        try
        {
            await UploadAsync(request, files, uploaded);
            int postId;
            await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await CommunityAccess.LockAsync(db, $"community:author:{actorId}");
                await PostingActorAsync(actorId, request.Type);
                existing = await db.Posts.SingleOrDefaultAsync(p => p.AuthorId == actorId && p.ClientRequestId == request.ClientRequestId);
                if (existing != null) { CheckRequest(existing, hash); postId = existing.Id; }
                else
                {
                    await AttachmentsAsync(actorId, request);
                    if (request.Type == "PASS" && await db.Posts.CountAsync(p => p.AuthorId == actorId && p.Type == "PASS" && p.CreatedAt >= DateTime.UtcNow.AddHours(-24)) >= 10)
                        throw new ServiceException(429, "Tối đa 10 bài PASS trong 24 giờ.", "PASS_DAILY_LIMIT");
                    var post = new Post { AuthorId = actorId, Type = request.Type, Title = request.Title, PostDescription = request.PostDescription,
                        ClientRequestId = request.ClientRequestId, RequestHash = hash };
                    if (request.PassInfo != null) { post.PassInfo = new PassPostInfo(); ApplyPass(post.PassInfo, request.PassInfo); }
                    foreach (var media in request.Media) post.Media.Add(NewMedia(media, uploaded[media.UploadKey!], request.Type));
                    await ConsumeUploadsAsync(uploaded.Values);
                    db.Posts.Add(post); await db.SaveChangesAsync(); postId = post.Id;
                }
                await tx.CommitAsync();
                attached = existing == null;
            }
            return await DetailAsync(postId, actorId);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || CommunityDatabaseErrors.IsConflict(ex))
        { throw new ServiceException(409, "Dữ liệu vừa thay đổi. Hãy thử lại.", "COMMUNITY_CONFLICT"); }
        finally { if (!attached) await ReleaseUploadsAsync(uploaded.Values); }
    }

    public async Task<CommunityPostDto> UpdateAsync(int postId, int actorId, CommunityPostWriteRequest request, IReadOnlyList<IFormFile> files)
    {
        CommunityPostRules.Validate(request, files, false);
        var before = await db.Posts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == postId)
            ?? throw new ServiceException(404, "Không tìm thấy bài viết.", "POST_NOT_FOUND");
        await EditableAsync(before, actorId, request.Type); CommunityAccess.Version(before, request.Version);
        await AttachmentsAsync(actorId, request);
        // Reject stolen IDs/type changes before spending time on uploads.
        var retained = request.Media.Where(m => m.Id.HasValue).ToList();
        var old = await db.PostMedia.AsNoTracking().Where(m => m.PostId == postId).ToListAsync();
        ValidateRetained(retained, old);
        var uploaded = new Dictionary<string, CommunityUploadedMedia>(); bool attached = false;
        try
        {
            await UploadAsync(request, files, uploaded);
            await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await CommunityAccess.LockAsync(db, $"community:post:{postId}");
                var post = await db.Posts.Include(p => p.Media).Include(p => p.PassInfo).SingleAsync(p => p.Id == postId);
                if (post.Type == "PASS" && await db.Set<PassOrderLock>().AnyAsync(l => l.PostId == postId)) throw new ServiceException(409, "Món PASS đang thuộc một đơn hàng.", "PASS_ORDER_LOCKED");
                await EditableAsync(post, actorId, request.Type); CommunityAccess.Version(post, request.Version);
                await AttachmentsAsync(actorId, request); ValidateRetained(retained, post.Media.ToList());
                var keep = retained.Select(m => m.Id!.Value).ToHashSet();
                foreach (var media in post.Media.ToList())
                {
                    if (!keep.Contains(media.Id))
                    {
                        db.CommunityMediaCleanup.Add(new() { PublicId = media.StoragePublicId, MediaType = media.MediaType, DeleteAfter = DateTime.UtcNow });
                        db.PostMedia.Remove(media); post.Media.Remove(media);
                    }
                    else media.DisplayOrder += 100; // First free unique positions before reordering.
                }
                await db.SaveChangesAsync();
                foreach (var m in request.Media)
                {
                    if (m.Id.HasValue)
                    {
                        var media = post.Media.Single(i => i.Id == m.Id.Value);
                        media.Caption = m.Caption; media.DisplayOrder = m.DisplayOrder; media.StoreId = m.StoreId; media.ProductId = m.ProductId;
                    }
                    else post.Media.Add(NewMedia(m, uploaded[m.UploadKey!], post.Type));
                }
                post.Title = request.Title; post.PostDescription = request.PostDescription; post.UpdatedAt = DateTime.UtcNow;
                if (post.Type == "PASS") ApplyPass(post.PassInfo ?? throw new ServiceException(409, "Bài PASS thiếu thông tin sản phẩm.", "COMMUNITY_CONFLICT"), request.PassInfo!);
                await ConsumeUploadsAsync(uploaded.Values);
                await db.SaveChangesAsync(); await tx.CommitAsync(); attached = true;
            }
            return await DetailAsync(postId, actorId);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || CommunityDatabaseErrors.IsConflict(ex))
        { throw new ServiceException(409, "Dữ liệu vừa thay đổi. Hãy tải lại.", "COMMUNITY_CONFLICT"); }
        finally { if (!attached) await ReleaseUploadsAsync(uploaded.Values); }
    }

    public async Task<CommunityPostDto> VisibilityAsync(int postId, int actorId, CommunityVisibilityRequest request)
    {
        if (request.VisibilityStatus is not ("ACTIVE" or "HIDDEN")) throw new ServiceException(400, "Trạng thái hiển thị không hợp lệ.", "INVALID_VISIBILITY");
        bool admin;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await CommunityAccess.LockAsync(db, $"community:post:{postId}");
            var actor = await CommunityAccess.ActorAsync(db, actorId); admin = actor.Role == "Admin";
            var post = await db.Posts.SingleOrDefaultAsync(p => p.Id == postId) ?? throw new ServiceException(404, "Không tìm thấy bài viết.", "POST_NOT_FOUND");
            if (!admin && post.Type == "PASS" && await db.Set<PassOrderLock>().AnyAsync(l => l.PostId == postId)) throw new ServiceException(409, "Món PASS đang thuộc một đơn hàng.", "PASS_ORDER_LOCKED");
            if (post.AuthorId != actorId && !admin) throw new ServiceException(403, "Không có quyền sửa bài.", "POST_NOT_OWNED");
            CommunityAccess.Version(post, request.Version);
            if (!admin && post.HiddenByAdmin) throw new ServiceException(403, "Bài đang bị quản trị viên ẩn.", "POST_MODERATED");
            post.VisibilityStatus = request.VisibilityStatus;
            if (admin) post.HiddenByAdmin = request.VisibilityStatus == "HIDDEN";
            post.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        return await DetailAsync(postId, actorId, admin);
    }
    public async Task<CommunityPostDto> SaleAsync(int postId, int actorId, CommunitySaleRequest request)
    {
        if (request.SaleStatus is not ("AVAILABLE" or "SOLD")) throw new ServiceException(400, "Trạng thái bán không hợp lệ.", "INVALID_SALE_STATUS");
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await CommunityAccess.LockAsync(db, $"community:post:{postId}");
            var post = await db.Posts.Include(p => p.PassInfo).SingleOrDefaultAsync(p => p.Id == postId) ?? throw new ServiceException(404, "Không tìm thấy bài viết.", "POST_NOT_FOUND");
            if (await db.Set<PassOrderLock>().AnyAsync(l => l.PostId == postId)) throw new ServiceException(409, "Món PASS đang thuộc một đơn hàng.", "PASS_ORDER_LOCKED");
            if (request.SaleStatus == "AVAILABLE")
            {
                var latestOrderId = await db.OrderItems.AsNoTracking().Where(i => i.PassPostId == postId)
                    .Join(db.Orders.AsNoTracking().Where(o => o.Status != "CANCELLED"), i => i.OrderId, o => o.Id, (i, o) => o)
                    .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id).Select(o => (int?)o.Id).FirstOrDefaultAsync();
                if (latestOrderId.HasValue && await db.ReturnRequests.AsNoTracking().AnyAsync(r => r.OrderId == latestOrderId && r.Status != "REJECTED" && !r.Events.Any(e => e.Action == "relist")))
                    throw new ServiceException(409, "Món đang xử lý trả hàng. Hãy kiểm tra và mở bán lại từ chi tiết đơn sau khi hoàn tiền.", "PASS_RETURN_PENDING");
            }
            await EditableAsync(post, actorId, "PASS"); CommunityAccess.Version(post, request.Version);
            post.PassInfo!.SaleStatus = request.SaleStatus; post.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        return await DetailAsync(postId, actorId);
    }
    private async Task PostingActorAsync(int id, string type)
    {
        var user = await CommunityAccess.ActorAsync(db, id);
        if (type == "PASS" && user.Role != "CasualUser") throw new ServiceException(403, "Chỉ CasualUser được đăng PASS.", "PASS_ROLE_REQUIRED");
        if (type == "OTHER" && user.Role is not ("CasualUser" or "Shopper")) throw new ServiceException(403, "Tài khoản không được đăng bài.", "FORBIDDEN");
    }
    private async Task EditableAsync(Post post, int actorId, string type)
    {
        await CommunityAccess.ActorAsync(db, actorId);
        if (post.AuthorId != actorId) throw new ServiceException(403, "Không có quyền sửa bài.", "POST_NOT_OWNED");
        if (post.Type != type) throw new ServiceException(400, "Không được đổi loại bài.", "POST_TYPE_IMMUTABLE");
        if (post.HiddenByAdmin) throw new ServiceException(403, "Bài đang bị quản trị viên ẩn.", "POST_MODERATED");
        await PostingActorAsync(actorId, type);
    }
    private async Task AttachmentsAsync(int actorId, CommunityPostWriteRequest request)
    {
        var user = await CommunityAccess.ActorAsync(db, actorId);
        foreach (var m in request.Media)
        {
            if (m.ProductId.HasValue)
            {
                var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == m.ProductId && p.IsPublished)
                    ?? throw new ServiceException(404, "Sản phẩm không còn công khai.", "ATTACHMENT_NOT_AVAILABLE");
                if (m.StoreId.HasValue && m.StoreId != product.StoreId) throw new ServiceException(400, "Sản phẩm không thuộc shop đã chọn.", "PRODUCT_STORE_MISMATCH");
                m.StoreId = product.StoreId;
            }
            if (m.StoreId.HasValue)
            {
                var store = await CommunityAccess.PublicStores(db).AsNoTracking().SingleOrDefaultAsync(s => s.Id == m.StoreId)
                    ?? throw new ServiceException(404, "Shop không còn công khai.", "ATTACHMENT_NOT_AVAILABLE");
                if (user.Role == "Shopper" && store.OwnerId != actorId) throw new ServiceException(403, "Shopper chỉ được gắn shop/sản phẩm của mình.", "ATTACHMENT_NOT_OWNED");
            }
        }
    }
    private async Task UploadAsync(CommunityPostWriteRequest request, IReadOnlyList<IFormFile> files, Dictionary<string, CommunityUploadedMedia> uploaded)
    {
        foreach (var file in files)
        {
            var type = request.Media.Single(m => m.UploadKey == file.Name).MediaType;
            await CommunityPostRules.ValidateFileAsync(file, type);
            var publicId = $"{(configuration["Cloudinary:Folder"] ?? "doosii").Trim('/')}/community/{Guid.NewGuid():N}";
            var pending = new CommunityMediaCleanup { PublicId = publicId, MediaType = type, DeleteAfter = DateTime.UtcNow.AddHours(24) };
            db.CommunityMediaCleanup.Add(pending); await db.SaveChangesAsync();
            // Record even an in-flight/failed upload: its public ID is known and retryable.
            uploaded.Add(file.Name, new(publicId, type, "", null));
            uploaded[file.Name] = await storage.UploadAsync(file, type, publicId);
        }
    }
    private async Task ConsumeUploadsAsync(IEnumerable<CommunityUploadedMedia> uploads)
    {
        foreach (var upload in uploads)
        {
            var pending = await db.CommunityMediaCleanup.SingleOrDefaultAsync(q => q.PublicId == upload.PublicId);
            if (pending == null || pending.DeleteAfter <= DateTime.UtcNow) throw new ServiceException(409, "Upload đã hết hạn. Hãy thử lại.", "COMMUNITY_CONFLICT");
            db.CommunityMediaCleanup.Remove(pending);
        }
    }
    private async Task ReleaseUploadsAsync(IEnumerable<CommunityUploadedMedia> uploads)
    {
        var ids = uploads.Select(u => u.PublicId).ToArray();
        if (ids.Length == 0) return;
        try { await db.CommunityMediaCleanup.Where(q => ids.Contains(q.PublicId)).ExecuteUpdateAsync(s => s.SetProperty(q => q.DeleteAfter, DateTime.UtcNow)); }
        catch (Exception ex) { logger.LogError(ex, "Could not expedite community cleanup; durable 24-hour cleanup remains scheduled"); }
    }
    private static void CheckRequest(Post post, string hash)
    {
        if (post.RequestHash != hash) throw new ServiceException(409, "Mã gửi bài đã dùng cho nội dung khác.", "CLIENT_REQUEST_CONFLICT");
    }
    private static void ValidateRetained(List<CommunityMediaWrite> retained, List<PostMedia> old)
    {
        if (retained.Any(m => !old.Any(i => i.Id == m.Id && i.MediaType == m.MediaType))) throw new ServiceException(400, "Media không thuộc bài hoặc không đúng loại.", "INVALID_MEDIA");
    }
    private static PostMedia NewMedia(CommunityMediaWrite m, CommunityUploadedMedia upload, string type) => new() {
        PostType = type, MediaType = m.MediaType, SourceUrl = upload.SourceUrl, StoragePublicId = upload.PublicId,
        ThumbnailUrl = upload.ThumbnailUrl, Caption = m.Caption, DisplayOrder = m.DisplayOrder, StoreId = m.StoreId, ProductId = m.ProductId };
    private static void ApplyPass(PassPostInfo target, CommunityPassInfo p)
    {
        target.ProductName = p.ProductName; target.ProductDescription = p.ProductDescription; target.Price = p.Price;
        target.Size = p.Size; target.ConditionPercent = p.ConditionPercent; target.Defects = p.Defects;
        target.StyleTagsJson = JsonSerializer.Serialize(p.StyleTagIds); target.MeasurementsJson = p.Measurements == null ? null : JsonSerializer.Serialize(p.Measurements);
    }
}
