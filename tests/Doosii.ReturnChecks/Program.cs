using Doosii.BLL.Services;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Forum;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

const string masterConnection = "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;Encrypt=false;Connect Timeout=5";
var name = "DoosiiReturnsTest_" + Guid.NewGuid().ToString("N");
await using var master = new SqlConnection(masterConnection);
await master.OpenAsync();
await using (var command = new SqlCommand($"CREATE DATABASE [{name}]", master)) await command.ExecuteNonQueryAsync();
var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = name }.ConnectionString).Options;
var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
async Task Reject<T>(Func<Task> operation, string message) where T : Exception
{
    try { await operation(); } catch (T) { checks++; return; }
    throw new Exception("Expected rejection: " + message);
}
try
{
    await using var db = new AppDbContext(options);
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261029164614_AddPassOrderLocks");
    var buyer = new User { Email = "return-buyer@example.invalid", FullName = "Buyer", PasswordHash = "x" };
    var seller = new User { Email = "return-seller@example.invalid", FullName = "Seller", PasswordHash = "x", Role = "Shopper" };
    var admin = new User { Email = "return-admin@example.invalid", FullName = "Admin", PasswordHash = "x", Role = "Admin" };
    var stranger = new User { Email = "return-other@example.invalid", FullName = "Other", PasswordHash = "x" };
    db.Users.AddRange(buyer, seller, admin, stranger); await db.SaveChangesAsync();
    var shop = new Store { OwnerId = seller.Id, Name = "Test shop" };
    var category = new Category { Name = "Return fixture", Slug = "return-fixture" };
    db.AddRange(shop, category); await db.SaveChangesAsync();
    var product = new Product { StoreId = shop.Id, CategoryId = category.Id, Title = "Shop item", Price = 100000 };
    var post = new Post { AuthorId = seller.Id, Type = "PASS", Title = "Pass item", PostDescription = "Fixture", ClientRequestId = Guid.NewGuid(), PassInfo = new PassPostInfo { ProductName = "Pass item", Price = 100000, Size = "M", Defects = "Không có" } };
    db.AddRange(product, post); await db.SaveChangesAsync();
    Order Fixture(bool pass, DateTime? delivered = null) => new() { BuyerId = buyer.Id, SellerId = seller.Id, PaymentMethod = "COD", Status = OrderStatus.CodCompleted, DeliveredAt = delivered ?? DateTime.UtcNow, CompletedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow, ReceiverName = "Receiver", ReceiverPhone = "0900000000", ShippingAddress = "Fixture", ProductPrice = 100000, TotalAmount = 100000, Items = new List<OrderItem> { pass ? new() { SourceType = "PASS_POST", PassPostId = post.Id, ProductNameSnapshot = "Pass item", SellerNameSnapshot = seller.FullName, PriceSnapshot = 100000 } : new() { ProductId = product.Id, ProductNameSnapshot = "Shop item", StoreIdSnapshot = shop.Id, StoreNameSnapshot = shop.Name, SellerNameSnapshot = seller.FullName, PriceSnapshot = 100000 } } };
    var oldOrder = Fixture(false); db.Orders.Add(oldOrder); await db.SaveChangesAsync(); var oldId = oldOrder.Id;
    await migrator.MigrateAsync(); db.ChangeTracker.Clear();
    Check(await db.Orders.AnyAsync(o => o.Id == oldId && o.Status == OrderStatus.CodCompleted), "upgrade preserves preexisting order");
    Check(await db.OrderItems.AnyAsync(i => i.OrderId == oldId && i.ProductNameSnapshot == "Shop item"), "upgrade preserves snapshots");
    Check(!db.Database.HasPendingModelChanges(), "migration matches model");
    var passOrder = Fixture(true); var expired = Fixture(false, DateTime.UtcNow.AddHours(-49)); var future = Fixture(false, DateTime.UtcNow.AddHours(1)); var concurrent = Fixture(false);
    db.Orders.AddRange(passOrder, expired, future, concurrent); await db.SaveChangesAsync();
    var passId = passOrder.Id; var expiredId = expired.Id; var futureId = future.Id;
    ReturnInput Request() => new() { Note = "Hàng khác mô tả", EvidenceUrl = "https://example.invalid/evidence.jpg" };
    async Task<ReturnRequest> Current(int id) { db.ChangeTracker.Clear(); return await db.ReturnRequests.AsNoTracking().SingleAsync(r => r.OrderId == id); }
    async Task Change(int id, int actor, string action, ReturnInput input) { db.ChangeTracker.Clear(); input.Version = Convert.ToBase64String((await Current(id)).RowVersion); await new ReturnService(db).Change(id, actor, action, input); }
    await Reject<UnauthorizedAccessException>(() => new ReturnService(db).Create(oldId, stranger.Id, Request()), "stranger cannot create"); db.ChangeTracker.Clear();
    await Reject<InvalidOperationException>(() => new ReturnService(db).Create(expiredId, buyer.Id, Request()), "48-hour deadline"); db.ChangeTracker.Clear();
    await Reject<InvalidOperationException>(() => new ReturnService(db).Create(futureId, buyer.Id, Request()), "future delivery"); db.ChangeTracker.Clear();
    await new ReturnService(db).Create(oldId, buyer.Id, Request()); db.ChangeTracker.Clear();
    Check(await db.Notifications.CountAsync(n => n.UserId == seller.Id && n.TargetUrl == $"/orders/{oldId}") == 1, "seller receives order notification on request");
    Check(await db.Notifications.CountAsync(n => n.UserId == seller.Id && n.TargetUrl == $"/orders/{oldId}") == 1, "seller receives order notification on request");
    var stale = Convert.ToBase64String((await Current(oldId)).RowVersion);
    await Reject<InvalidOperationException>(() => new ReturnService(db).Create(oldId, buyer.Id, Request()), "one request per order"); db.ChangeTracker.Clear();
    await Reject<UnauthorizedAccessException>(() => new ReturnService(db).Get(oldId, stranger.Id), "stranger cannot read"); db.ChangeTracker.Clear();
    await Reject<InvalidOperationException>(() => Change(oldId, seller.Id, "approve", new ReturnInput { FeePayer = "SELLER", RefundAmount = 0 }), "zero refund");
    await Reject<InvalidOperationException>(() => Change(oldId, buyer.Id, "confirm-refund", new ReturnInput()), "cannot confirm before transfer evidence");
    await Change(oldId, seller.Id, "approve", new ReturnInput { FeePayer = "SELLER", RefundAmount = 100000 });
    db.ChangeTracker.Clear();
    await Reject<InvalidOperationException>(() => new ReturnService(db).Change(oldId, buyer.Id, "ship", new ReturnInput { Version = stale, Carrier = "Fixture", TrackingCode = "STALE" }), "stale version rejected");
    await Change(oldId, buyer.Id, "ship", new ReturnInput { Carrier = "Fixture", TrackingCode = "TRACK" });
    await Change(oldId, buyer.Id, "escalate", new ReturnInput { Note = "Cần xem xét" });
    Check(await db.Notifications.AnyAsync(n => n.UserId == admin.Id && n.TargetUrl == $"/orders/{oldId}"), "escalation notifies admin");
    Check(await db.Notifications.AnyAsync(n => n.UserId == admin.Id && n.TargetUrl == $"/orders/{oldId}"), "escalation notifies admin");
    await Reject<InvalidOperationException>(() => Change(oldId, admin.Id, "admin-reject", new ReturnInput { Note = "Không được từ chối sau khi gửi" }), "cannot strand return shipment");
    await Change(oldId, admin.Id, "admin-resume", new ReturnInput { Note = "Tiếp tục vận chuyển" });
    await Change(oldId, seller.Id, "receive", new ReturnInput());
    await Change(oldId, seller.Id, "refund-proof", new ReturnInput { EvidenceUrl = "https://example.invalid/refund.jpg" });
    Check((await Current(oldId)).Status == "REFUND_PENDING", "proof alone is not confirmation");
    await Change(oldId, buyer.Id, "confirm-refund", new ReturnInput());
    Check(await db.Notifications.AnyAsync(n => n.UserId == seller.Id && n.TargetUrl == $"/orders/{oldId}" && n.Message.Contains("xác nhận nhận tiền")), "seller receives refund confirmation");
    Check(await db.Notifications.AnyAsync(n => n.UserId == seller.Id && n.TargetUrl == $"/orders/{oldId}" && n.Message.Contains("xác nhận nhận tiền")), "seller receives refund confirmation");
    var complete = await Current(oldId); Check(complete.Status == "REFUND_COMPLETED" && complete.RefundConfirmedAt.HasValue, "buyer confirms refund");
    Check(await db.Orders.AsNoTracking().Where(o => o.Id == oldId).Select(o => o.Status).SingleAsync() == OrderStatus.CodCompleted, "order history remains COD completed");
    db.ChangeTracker.Clear(); await new ReturnService(db).Create(passId, buyer.Id, Request());
    await Change(passId, seller.Id, "reject", new ReturnInput { Note = "Không đồng ý" });
    await Change(passId, buyer.Id, "escalate", new ReturnInput { Note = "Nhờ Admin kiểm tra" });
    await Change(passId, admin.Id, "admin-approve", new ReturnInput { Note = "Đã kiểm tra", FeePayer = "SELLER", RefundAmount = 100000 });
    Check((await Current(passId)).Status == "APPROVED", "PASS rejection can be reviewed and approved");
    Check(await db.Set<PassPostInfo>().AsNoTracking().AnyAsync(p => p.PostId == post.Id), "PASS product preserved");
    async Task<bool> Race()
    {
        await using var workerDb = new AppDbContext(options);
        try { await new ReturnService(workerDb).Create(concurrent.Id, buyer.Id, Request()); return true; }
        catch (InvalidOperationException) { return false; }
    }
    var results = await Task.WhenAll(Race(), Race());
    Check(results.Count(success => success) == 1, "concurrent creation has exactly one winner");
    db.ChangeTracker.Clear();
    Check(await db.ReturnRequests.CountAsync(r => r.OrderId == concurrent.Id) == 1, "concurrent creation persisted exactly one request");
    Console.WriteLine($"PASS: {checks} return migration, authorization, deadline and COD workflow checks.");
}
finally
{
    SqlConnection.ClearAllPools();
    const string prefix = "DoosiiReturnsTest_";
    if (!name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _)) throw new Exception("Unexpected cleanup target");
    await using var cleanup = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]", master);
    await cleanup.ExecuteNonQueryAsync();
    Console.WriteLine("Removed isolated test database " + name);
}
