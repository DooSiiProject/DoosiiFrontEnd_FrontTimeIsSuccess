using Doosii.DAL.Models.Order;
namespace Doosii.BLL.Services;
internal static class DisputeRules
{
    public static DateTime? Deadline(Order order)
    {
        var received = order.DeliveredAt ?? (order.Status == OrderStatus.CompletedReleased ? order.CompletedAt : null);
        return received.HasValue ? DateTime.SpecifyKind(received.Value, DateTimeKind.Utc).AddHours(24) : null;
    }
    public static bool Eligible(Order order, DateTime now)
    {
        if (order.Dispute != null || order.Status is not (OrderStatus.InTransit or OrderStatus.CompletedReleased)) return false;
        var deadline = Deadline(order);
        return order.Status == OrderStatus.InTransit && deadline == null || deadline.HasValue && now <= deadline;
    }
    public static string? EvidenceUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var url = value.Trim();
        if (url.Length > 500 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Bằng chứng phải là đường dẫn HTTPS hợp lệ, tối đa 500 ký tự.");
        return url;
    }
}
