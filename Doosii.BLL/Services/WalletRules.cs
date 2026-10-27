using Microsoft.EntityFrameworkCore;
using Doosii.BLL.Exceptions;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
namespace Doosii.BLL.Services;

internal static class WalletRules
{
    // Every wallet debit uses the same seller lock in the caller's transaction.
    public static async Task<User> LockAsync(AppDbContext db, int userId)
    {
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [auth].[Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}").SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy người bán.");
        await db.Entry(user).ReloadAsync();
        // Order transitions use these same row locks. Take them before evaluating maturity/balance.
        await db.Orders.FromSqlInterpolated($"SELECT * FROM [dbo].[Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [SellerId] = {userId}").AsNoTracking().ToListAsync();
        return user;
    }
    public static async Task EnsureSellerAsync(AppDbContext db, int userId)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role == "Shopper"))
            throw new ServiceException(403, "Ví dành cho tài khoản Shopper đang hoạt động.", "SELLER_REQUIRED");
    }
}
