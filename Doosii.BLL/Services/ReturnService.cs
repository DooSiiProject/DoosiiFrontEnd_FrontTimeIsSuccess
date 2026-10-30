using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Doosii.BLL.Hubs;
using Doosii.DAL.Models.Notification;

namespace Doosii.BLL.Services;

public sealed class ReturnInput
{
    [MaxLength(1000)] public string? Note { get; set; }
    [MaxLength(500)] public string? EvidenceUrl { get; set; }
    [MaxLength(100)] public string? Carrier { get; set; }
    [MaxLength(100)] public string? TrackingCode { get; set; }
    [MaxLength(20)] public string? FeePayer { get; set; }
    public decimal? RefundAmount { get; set; }
    [MaxLength(100)] public string? Version { get; set; }
}

public sealed class ReturnService(AppDbContext db, IHubContext<NotificationHub>? hub = null)
{
    private async Task<List<int>> AddNotifications(Order order, int actor, string action, string status, DateTime now)
    {
        var message = action switch {
            "create" => "Người mua đã yêu cầu trả hàng. Hãy xem và phản hồi.",
            "approve" or "admin-approve" => "Yêu cầu trả hàng đã được duyệt. Người mua hãy kiểm tra phương án hoàn tiền và gửi hàng trả.",
            "reject" or "admin-reject" => "Yêu cầu trả hàng đã bị từ chối. Hãy xem lý do trong chi tiết đơn.",
            "ship" => "Người mua đã ghi nhận gửi hàng trả. Người bán hãy theo dõi vận đơn.",
            "receive" => "Người bán đã xác nhận nhận hàng trả và cần cung cấp bằng chứng hoàn tiền.",
            "refund-proof" => "Người bán đã gửi bằng chứng hoàn tiền. Người mua hãy kiểm tra tiền thực nhận.",
            "confirm-refund" => "Người mua đã xác nhận nhận tiền hoàn.",
            "escalate" => "Yêu cầu trả hàng đã được chuyển đến Admin xem xét.",
            "admin-resume" => "Admin đã yêu cầu tiếp tục xử lý trả hàng. Hãy xem chi tiết đơn.",
            "admin-confirm-refund" => "Admin đã xác nhận kết quả xử lý hoàn tiền. Hãy xem giải trình.",
            _ => throw new InvalidOperationException("Thao tác thông báo không hợp lệ.")
        };
        var targets = new HashSet<int>();
        if (action == "create") targets.Add(order.SellerId);
        else if (action.StartsWith("admin-", StringComparison.Ordinal)) { targets.Add(order.BuyerId); targets.Add(order.SellerId); }
        else { targets.Add(actor == order.BuyerId ? order.SellerId : order.BuyerId); }
        if (action == "escalate")
            foreach (var id in await db.Users.Where(u => u.Role == "Admin" && u.Id != order.BuyerId && u.Id != order.SellerId).Select(u => u.Id).ToListAsync()) targets.Add(id);
        targets.Remove(actor);
        foreach (var id in targets) db.Notifications.Add(new Notification { UserId = id, Title = $"Trả hàng đơn #{order.Id}", Message = message, Type = NotificationType.OrderUpdate, TargetUrl = $"/orders/{order.Id}", CreatedAt = now });
        return targets.ToList();
    }
    private async Task PushChanged(IEnumerable<int> ids)
    {
        if (hub == null) return;
        foreach (var id in ids)
        {
            try { await hub.Clients.Group($"user_{id}").SendAsync("NotificationsChanged", new { Reason = "return" }); }
            catch { /* The committed notification remains available through REST. */ }
        }
    }
    private static string Required(string? value, int max, string label)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > max) throw new InvalidOperationException($"{label} là bắt buộc và tối đa {max} ký tự.");
        return text;
    }
    private static string Evidence(string? value)
    {
        var url = Required(value, 500, "Đường dẫn bằng chứng");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Bằng chứng phải là đường dẫn HTTPS hợp lệ.");
        return url;
    }
    private async Task<bool> IsAdmin(int id) => await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.Role == "Admin");
    private static object View(ReturnRequest r) => new { r.Id, r.OrderId, r.BuyerId, r.Reason, r.EvidenceJson, r.Status, r.SellerNote, r.ReturnCarrier, r.ReturnTrackingCode, r.RefundEvidenceUrl, r.AdminNote, r.ShippingFeePayer, r.RefundAmount, r.CreatedAt, r.DeadlineAt, r.UpdatedAt, r.ReturnedAt, r.RefundConfirmedAt, Version = Convert.ToBase64String(r.RowVersion), Events = r.Events.OrderBy(e => e.Id).Select(e => new { e.ActorId, e.Action, e.Note, e.CreatedAt }).ToList() };
    public async Task<object?> Get(int orderId, int actor)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId) ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == actor && u.Role != "Banned")) throw new UnauthorizedAccessException();
        if (actor != order.BuyerId && actor != order.SellerId && !await IsAdmin(actor)) throw new UnauthorizedAccessException();
        var r = await db.ReturnRequests.AsNoTracking().Include(x => x.Events).SingleOrDefaultAsync(x => x.OrderId == orderId);
        return r == null ? null : View(r);
    }
    public async Task<object> ListAdmin(int actor)
    {
        if (!await IsAdmin(actor)) throw new UnauthorizedAccessException();
        var items = await db.ReturnRequests.AsNoTracking().Include(r => r.Events).Where(r => r.Status == "ADMIN_REVIEW").OrderBy(r => r.UpdatedAt).Take(100).ToListAsync();
        return items.Select(View).ToList();
    }
    public async Task<object> Create(int orderId, int actor, ReturnInput input)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var order = await OrderLifecycle.LockAsync(db, orderId);
        if (actor != order.BuyerId || order.Buyer.Role == "Banned") throw new UnauthorizedAccessException();
        if (order.PaymentMethod != "COD" || order.Status != OrderStatus.CodCompleted || !order.DeliveredAt.HasValue)
            throw new InvalidOperationException("Chỉ hỗ trợ yêu cầu cho đơn COD đã ghi nhận giao thành công.");
        var delivered = DateTime.SpecifyKind(order.DeliveredAt.Value, DateTimeKind.Utc);
        if (delivered > DateTime.UtcNow) throw new InvalidOperationException("Thời điểm giao hàng không hợp lệ; hãy liên hệ hỗ trợ.");
        var deadline = delivered.AddHours(48);
        if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Đã quá 48 giờ kể từ khi ghi nhận giao hàng.");
        if (await db.ReturnRequests.AnyAsync(r => r.OrderId == orderId)) throw new InvalidOperationException("Đơn đã có yêu cầu trả hàng; hãy mở yêu cầu đó để tiếp tục.");
        var now = DateTime.UtcNow;
        var r = new ReturnRequest { OrderId = orderId, BuyerId = actor, Reason = Required(input.Note, 1000, "Lý do"), EvidenceJson = JsonSerializer.Serialize(new[] { Evidence(input.EvidenceUrl) }), CreatedAt = now, UpdatedAt = now, DeadlineAt = deadline };
        r.Events.Add(new ReturnRequestEvent { ActorId = actor, Action = "REQUESTED", Note = r.Reason, CreatedAt = now });
        db.ReturnRequests.Add(r);
        var targets = await AddNotifications(order, actor, "create", r.Status, now);
        await UploadedImageGuard.SaveChangesAsync(db); await tx.CommitAsync(); await PushChanged(targets); return View(r);
    }
    public async Task<object> Change(int orderId, int actor, string action, ReturnInput input)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var order = await OrderLifecycle.LockAsync(db, orderId);
        var r = await db.ReturnRequests.Include(x => x.Events).SingleOrDefaultAsync(x => x.OrderId == orderId) ?? throw new KeyNotFoundException("Chưa có yêu cầu trả hàng.");
        if (input.Version != Convert.ToBase64String(r.RowVersion)) throw new InvalidOperationException("Yêu cầu đã thay đổi, vui lòng tải lại.");
        var buyer = actor == order.BuyerId && order.Buyer.Role != "Banned";
        var seller = actor == order.SellerId && order.Seller.Role != "Banned";
        var admin = actor != order.BuyerId && actor != order.SellerId && await IsAdmin(actor);
        if (!buyer && !seller && !admin) throw new UnauthorizedAccessException();
        var now = DateTime.UtcNow;
        string? note = null;
        switch (action)
        {
            case "approve" when seller && r.Status == "REQUESTED":
                if (input.FeePayer is not ("BUYER" or "SELLER")) throw new InvalidOperationException("Chọn bên chịu phí vận chuyển.");
                if (input.RefundAmount is null || input.RefundAmount <= 0 || input.RefundAmount > order.TotalAmount || decimal.Truncate(input.RefundAmount.Value) != input.RefundAmount) throw new InvalidOperationException("Số tiền hoàn phải lớn hơn 0 và không vượt tổng tiền đơn.");
                r.ShippingFeePayer = input.FeePayer; r.RefundAmount = input.RefundAmount; r.Status = "APPROVED"; break;
            case "reject" when seller && r.Status == "REQUESTED": r.SellerNote = note = Required(input.Note, 1000, "Lý do từ chối"); r.Status = "REJECTED"; break;
            case "ship" when buyer && r.Status == "APPROVED": r.ReturnCarrier = Required(input.Carrier, 100, "Đơn vị vận chuyển"); r.ReturnTrackingCode = Required(input.TrackingCode, 100, "Mã vận đơn"); r.Status = "RETURN_SHIPPED"; break;
            case "receive" when seller && r.Status == "RETURN_SHIPPED": r.ReturnedAt = now; r.Status = "RETURN_RECEIVED"; break;
            case "refund-proof" when seller && r.Status == "RETURN_RECEIVED": r.RefundEvidenceUrl = Evidence(input.EvidenceUrl); r.Status = "REFUND_PENDING"; break;
            case "confirm-refund" when buyer && r.Status == "REFUND_PENDING": r.RefundConfirmedAt = now; r.Status = "REFUND_COMPLETED"; break;
            case "escalate" when (buyer || seller) && r.Status is "REQUESTED" or "REJECTED" or "APPROVED" or "RETURN_SHIPPED" or "RETURN_RECEIVED" or "REFUND_PENDING":
                note = Required(input.Note, 1000, "Nội dung bất đồng"); r.Status = "ADMIN_REVIEW"; break;
            case "admin-approve" when admin && r.Status == "ADMIN_REVIEW":
                note = Required(input.Note, 1000, "Giải trình");
                if (r.ReturnTrackingCode != null) throw new InvalidOperationException("Hàng đã được gửi trả: hãy tiếp tục xử lý theo hồ sơ thực tế.");
                if (input.FeePayer is not ("BUYER" or "SELLER") || input.RefundAmount is null || input.RefundAmount <= 0 || input.RefundAmount > order.TotalAmount || decimal.Truncate(input.RefundAmount.Value) != input.RefundAmount) throw new InvalidOperationException("Chọn bên chịu phí và số tiền hoàn hợp lệ.");
                r.AdminNote = note; r.ShippingFeePayer = input.FeePayer; r.RefundAmount = input.RefundAmount; r.Status = "APPROVED"; break;
            case "admin-reject" when admin && r.Status == "ADMIN_REVIEW" && r.ReturnTrackingCode == null: note = Required(input.Note, 1000, "Giải trình"); r.AdminNote = note; r.Status = "REJECTED"; break;
            case "admin-resume" when admin && r.Status == "ADMIN_REVIEW" && r.ReturnTrackingCode != null:
                note = Required(input.Note, 1000, "Giải trình"); r.AdminNote = note;
                r.Status = !r.ReturnedAt.HasValue ? "RETURN_SHIPPED" : r.RefundEvidenceUrl == null ? "RETURN_RECEIVED" : "REFUND_PENDING"; break;
            case "admin-confirm-refund" when admin && r.Status == "ADMIN_REVIEW" && r.ReturnedAt.HasValue && r.RefundEvidenceUrl != null:
                note = Required(input.Note, 1000, "Giải trình xác minh hoàn tiền"); r.AdminNote = note; r.RefundConfirmedAt = now; r.Status = "REFUND_COMPLETED"; break;
            default: throw new InvalidOperationException("Thao tác không phù hợp với trạng thái hoặc quyền hiện tại.");
        }
        r.UpdatedAt = now; r.Events.Add(new ReturnRequestEvent { ActorId = actor, Action = action, Note = note, CreatedAt = now });
        var targets = await AddNotifications(order, actor, action, r.Status, now);
        await UploadedImageGuard.SaveChangesAsync(db); await tx.CommitAsync(); await PushChanged(targets); return View(r);
    }
}
