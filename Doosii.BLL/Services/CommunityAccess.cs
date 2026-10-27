using System.Data;
using Doosii.BLL.Exceptions;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Forum;
using Microsoft.EntityFrameworkCore;

namespace Doosii.BLL.Services;

internal static class CommunityAccess
{
    internal static IQueryable<Post> PublicPosts(AppDbContext db) => db.Posts.Where(p => p.VisibilityStatus == "ACTIVE" && p.Author.Role != "Banned");
    internal static IQueryable<Doosii.DAL.Models.Store.Store> PublicStores(AppDbContext db) => db.Stores.Where(s => s.IsActive && s.Owner.Role == "Shopper" && s.Owner.MerchantProfile != null && s.Owner.MerchantProfile.KycStatus == "APPROVED");
    internal static async Task<User> ActorAsync(AppDbContext db, int id, bool admin = false)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id)
            ?? throw new ServiceException(401, "Vui lòng đăng nhập.", "UNAUTHORIZED");
        if (user.Role == "Banned") throw new ServiceException(403, "Tài khoản đã bị khóa.", "USER_BANNED");
        if (admin && user.Role != "Admin") throw new ServiceException(403, "Cần quyền quản trị.", "FORBIDDEN");
        return user;
    }
    internal static async Task LockAsync(AppDbContext db, string resource)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000;
            IF @result < 0 THROW 51001, 'Community write conflict', 1;
            """);
    }
    internal static void Version(Post post, string? version)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(version ?? ""); }
        catch (FormatException) { throw new ServiceException(400, "Version không hợp lệ.", "INVALID_VERSION"); }
        if (bytes.Length != 8) throw new ServiceException(400, "Version không hợp lệ.", "INVALID_VERSION");
        if (!post.RowVersion.SequenceEqual(bytes)) throw new ServiceException(409, "Bài viết vừa thay đổi. Hãy tải lại.", "COMMUNITY_CONFLICT");
    }
    internal static void Pagination(int page, int pageSize)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ServiceException(400, "Phân trang không hợp lệ.", "INVALID_PAGINATION");
    }
}
