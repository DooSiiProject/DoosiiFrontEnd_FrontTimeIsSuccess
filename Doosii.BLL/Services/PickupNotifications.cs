using Doosii.DAL.Data;
using Doosii.DAL.Models.Notification;
using Doosii.DAL.Models.Order;

namespace Doosii.BLL.Services;

internal static class PickupNotifications
{
    // Called inside the transaction that changes the reservation. Messages contain
    // no contact data, pickup code, ciphertext or user-entered notes/reasons.
    internal static void State(AppDbContext db, PickupReservation row, DateTime now)
    {
        var label = row.Status switch { "PENDING" => "đang chờ shop duyệt", "ACCEPTED" => "đã được shop duyệt",
            "REJECTED" => "đã bị từ chối", "CANCELLED" => "đã hủy", "EXPIRED" => "đã hết hạn", "COMPLETED" => "đã nhận tại shop", _ => throw new InvalidOperationException("Unknown pickup state") };
        foreach (var userId in new[] { row.BuyerId, row.SellerId }.Distinct())
            Queue(db, userId, $"pickup:{row.Id}:{row.Status}:{userId}", "Cập nhật lượt giữ đồ",
                $"Lượt giữ #{row.Id} {label}. Mở chi tiết để xem thông tin.", row.Id, now);
    }
    internal static void Location(AppDbContext db, PickupReservation row, int relocationId, DateTime now) =>
        Queue(db, row.BuyerId, $"pickup:{row.Id}:location:{relocationId}:{row.BuyerId}", "Shop đã thay đổi vị trí",
            $"Shop của lượt giữ #{row.Id} đã thay đổi địa điểm. Kiểm tra chi tiết và liên hệ shop; bạn có thể hủy lượt giữ.", row.Id, now);
    private static void Queue(AppDbContext db, int userId, string key, string title, string message, int reservationId, DateTime now)
    {
        db.PickupNotificationOutbox.Add(new() { EventKey = key, CreatedAt = now, AvailableAt = now,
            Notification = new Notification { UserId = userId, Title = title, Message = message, Type = "PICKUP_RESERVATION",
                TargetUrl = $"/holds?reservationId={reservationId}", CreatedAt = now } });
    }
}
