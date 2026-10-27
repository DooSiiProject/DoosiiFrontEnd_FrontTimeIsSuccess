using Microsoft.EntityFrameworkCore;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;
namespace Doosii.BLL.Services;

internal static class OrderLifecycle
{
    public static DateTime ExpiresAt(Order order) => DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc).AddMinutes(15);
    public static async Task<Order> LockAsync(AppDbContext db, int id)
    {
        // Caller holds a transaction; every payment/expiry/fulfilment transition takes the same lock.
        await db.Orders.FromSqlInterpolated($"SELECT * FROM [dbo].[Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").LoadAsync();
        return await db.Orders.Include(o => o.Buyer).Include(o => o.Seller).Include(o => o.Items).Include(o => o.EscrowTransactions).Include(o => o.Dispute).SingleOrDefaultAsync(o => o.Id == id)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");
    }
    public static void EnsureVersion(Order order, string version)
    {
        if (Convert.ToBase64String(order.RowVersion) != version) throw new InvalidOperationException("Đơn hàng đã thay đổi. Vui lòng tải lại trước khi thao tác.");
    }
    public static async Task<bool> ExpireAsync(AppDbContext db, Order order)
    {
        if (order.Status != OrderStatus.PendingPayment || ExpiresAt(order) > DateTime.UtcNow) return false;
        order.Status = OrderStatus.Cancelled; order.UpdatedAt = DateTime.UtcNow;
        foreach (var item in order.Items)
        {
            var product = await db.Products.FromSqlInterpolated($"SELECT * FROM [store].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {item.ProductId}").SingleOrDefaultAsync();
            if (product?.Status == "LOCKED" && product.ReservedOrderId == order.Id)
            {
                product.Status = "AVAILABLE"; product.ReservedOrderId = null; product.UpdatedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync(); return true;
    }
}
