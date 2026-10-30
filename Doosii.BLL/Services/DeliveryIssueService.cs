using System.ComponentModel.DataAnnotations;
using Doosii.BLL.Hubs;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;
using Doosii.DAL.Models.Notification;
using Doosii.DAL.Models.Order;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Doosii.BLL.Services;

public sealed class DeliveryIssueInput
{
    [MaxLength(1000)] public string? Note { get; set; }
    [MaxLength(500)] public string? EvidenceUrl { get; set; }
    [MaxLength(100)] public string? Version { get; set; }
}
public sealed class DeliveryIssueService(AppDbContext db, IHubContext<NotificationHub>? hub = null)
{
    private static object View(DeliveryIssue i) => new { i.OrderId, i.Status, i.Reason, i.EvidenceUrl, i.CreatedAt, i.UpdatedAt, Version = Convert.ToBase64String(i.RowVersion), Events = i.Events.OrderBy(e => e.Id).Select(e => new { e.Action, e.ActorId, e.Note, e.EvidenceUrl, e.CreatedAt }).ToArray() };
    public async Task<object?> Get(int id, int actor)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id) ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor);
        if (user == null || user.Role == "Banned" || (actor != order.BuyerId && actor != order.SellerId && user.Role != "Admin")) throw new UnauthorizedAccessException();
        var issue = await db.DeliveryIssues.AsNoTracking().Include(i => i.Events).SingleOrDefaultAsync(i => i.OrderId == id);
        return issue == null ? null : View(issue);
    }
    public async Task<object> Change(int id, int actor, string action, DeliveryIssueInput input)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var order = await OrderLifecycle.LockAsync(db, id);
        if (actor != order.SellerId || order.Seller.Role == "Banned") throw new UnauthorizedAccessException();
        if (order.PaymentMethod != "COD" || order.Status != OrderStatus.CodInTransit) throw new InvalidOperationException("Chỉ xử lý đơn COD đang giao.");
        if (order.PaidAt.HasValue || order.EscrowTransactions.Count != 0) throw new InvalidOperationException("Đơn đã có thông tin thanh toán; cần đối soát trước khi xử lý giao thất bại.");
        var issue = await db.DeliveryIssues.Include(i => i.Events).SingleOrDefaultAsync(i => i.OrderId == id);
        if (issue == null) OrderLifecycle.EnsureVersion(order, input.Version ?? "");
        else if (input.Version != Convert.ToBase64String(issue.RowVersion)) throw new InvalidOperationException("Thông tin đã thay đổi. Hãy tải lại.");
        var now = DateTime.UtcNow;
        var note = input.Note?.Trim();
        if (action == "fail")
        {
            if (issue != null && issue.Status != "RETRYING") throw new InvalidOperationException("Đơn đã có lần giao thất bại đang xử lý.");
            if (string.IsNullOrEmpty(note) || note.Length > 1000) throw new InvalidOperationException("Cần ghi lý do giao thất bại, tối đa 1000 ký tự.");
            var url = input.EvidenceUrl?.Trim();
            if (!string.IsNullOrEmpty(url) && (url.Length > 500 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo))) throw new InvalidOperationException("Bằng chứng phải là liên kết HTTPS hợp lệ.");
            if (issue == null) { issue = new DeliveryIssue { OrderId = id, CreatedAt = now }; db.DeliveryIssues.Add(issue); }
            issue.Reason = note; issue.EvidenceUrl = string.IsNullOrEmpty(url) ? null : url; issue.Status = "FAILED";
        }
        else if (action == "retry" && issue?.Status == "FAILED") issue.Status = "RETRYING";
        else if (action == "return" && issue?.Status == "FAILED") issue.Status = "RETURNING";
        else if (action == "receive" && issue?.Status == "RETURNING")
        {
            if (order.Items.Count != 1) throw new InvalidOperationException("Chỉ hỗ trợ đơn có một món; liên hệ hỗ trợ.");
            var item = order.Items.Single();
            if (item.SourceType == "SHOP_PRODUCT" && item.ProductId is int productId)
            {
                var product = await db.Products.FromSqlInterpolated($"SELECT * FROM [store].[Products] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {productId}").SingleOrDefaultAsync();
                if (product == null || product.Status != "LOCKED" || product.ReservedOrderId != id || product.ReservedPickupReservationId != null || !await db.Stores.AnyAsync(s => s.Id == product.StoreId && s.OwnerId == actor)) throw new InvalidOperationException("Món hàng không còn thuộc lượt giữ của đơn này.");
                product.Status = "AVAILABLE"; product.IsPublished = false; product.ReservedOrderId = null; product.UpdatedAt = now;
            }
            else if (item.SourceType == "PASS_POST" && item.PassPostId is int postId)
            {
                await CommunityAccess.LockAsync(db, $"community:post:{postId}");
                var held = await db.Set<PassOrderLock>().SingleOrDefaultAsync(l => l.PostId == postId && l.OrderId == id) ?? throw new InvalidOperationException("Món PASS không còn thuộc đơn này.");
                var post = await db.Posts.Include(p => p.PassInfo).SingleOrDefaultAsync(p => p.Id == postId && p.Type == "PASS" && p.AuthorId == actor);
                if (post?.PassInfo == null || post.PassInfo.SaleStatus != "AVAILABLE") throw new InvalidOperationException("Tình trạng món PASS đã thay đổi.");
                post.VisibilityStatus = "HIDDEN"; post.UpdatedAt = now; db.Remove(held);
            }
            else throw new InvalidOperationException("Nguồn món hàng không hợp lệ.");
            issue.Status = "RETURNED"; order.Status = OrderStatus.CodReturned; order.UpdatedAt = now;
        }
        else throw new InvalidOperationException("Thao tác không phù hợp với trạng thái hiện tại.");
        issue!.UpdatedAt = now;
        issue.Events.Add(new DeliveryIssueEvent { ActorId = actor, Action = action, Note = note, EvidenceUrl = action == "fail" ? issue.EvidenceUrl : null, CreatedAt = now });
        var message = action switch { "fail" => "Người bán ghi nhận giao hàng thất bại; hãy xem lý do và liên hệ người bán.", "retry" => "Người bán ghi nhận sẽ thử giao lại.", "return" => "Người bán ghi nhận hàng đang hoàn về.", _ => "Người bán xác nhận đã nhận lại hàng. Đơn giao không thành công." };
        db.Notifications.Add(new Notification { UserId = order.BuyerId, Title = $"Vận chuyển đơn #{id}", Message = message, Type = NotificationType.OrderUpdate, TargetUrl = $"/orders/{id}", CreatedAt = now });
        await UploadedImageGuard.SaveChangesAsync(db); await tx.CommitAsync();
        if (hub != null) try { await hub.Clients.Group($"user_{order.BuyerId}").SendAsync("NotificationsChanged", new { Reason = "delivery" }); } catch { }
        return View(issue);
    }
}
