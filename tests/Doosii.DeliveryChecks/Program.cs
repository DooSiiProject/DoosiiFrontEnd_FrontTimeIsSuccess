using Doosii.BLL.DTOs;
using Doosii.BLL.Services;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Forum;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

const string masterConnection = "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;Encrypt=false;Connect Timeout=5";
var name = "DoosiiDeliveryTest_" + Guid.NewGuid().ToString("N");
await using var master = new SqlConnection(masterConnection); await master.OpenAsync();
await using (var command = new SqlCommand($"CREATE DATABASE [{name}]", master)) await command.ExecuteNonQueryAsync();
var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = name }.ConnectionString).Options;
int count = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
async Task Reject<T>(Func<Task> action, string message) where T : Exception { try { await action(); } catch (T) { count++; return; } throw new Exception("Expected rejection: " + message); }
try
{
    await using var db = new AppDbContext(options); await db.Database.MigrateAsync();
    var buyer = new User { Email = "delivery-buyer@example.invalid", FullName = "Buyer", PasswordHash = "x" };
    var seller = new User { Email = "delivery-seller@example.invalid", FullName = "Seller", Role = "Shopper", PasswordHash = "x" };
    var passSeller = new User { Email = "delivery-pass@example.invalid", FullName = "Pass seller", PasswordHash = "x" };
    var outsider = new User { Email = "delivery-outsider@example.invalid", FullName = "Other", PasswordHash = "x" };
    db.Users.AddRange(buyer, seller, passSeller, outsider); await db.SaveChangesAsync();
    var shop = new Store { OwnerId = seller.Id, Name = "Shop" }; var category = new Category { Name = "Fixture", Slug = "delivery-fixture" };
    db.AddRange(shop, category); await db.SaveChangesAsync();
    var product = new Product { StoreId = shop.Id, CategoryId = category.Id, Title = "Item", Price = 100000 };
    var post = new Post { AuthorId = passSeller.Id, Type = "PASS", Title = "PASS", PostDescription = "Fixture", ClientRequestId = Guid.NewGuid(), PassInfo = new PassPostInfo { ProductName = "PASS", Price = 100000, Size = "M", Defects = "Không có" } };
    db.AddRange(product, post); await db.SaveChangesAsync();
    Order Fixture(int sellerId, OrderItem item) => new() { BuyerId = buyer.Id, SellerId = sellerId, PaymentMethod = "COD", Status = OrderStatus.CodInTransit, ShippedAt = DateTime.UtcNow, ReceiverName = "Test", ReceiverPhone = "0900000000", ShippingAddress = "Fixture", ProductPrice = 100000, TotalAmount = 100000, Items = new List<OrderItem> { item } };
    var shopOrder = Fixture(seller.Id, new OrderItem { ProductId = product.Id, ProductNameSnapshot = "Item", PriceSnapshot = 100000, StoreIdSnapshot = shop.Id, StoreNameSnapshot = shop.Name, SellerNameSnapshot = seller.FullName });
    var passOrder = Fixture(passSeller.Id, new OrderItem { SourceType = "PASS_POST", PassPostId = post.Id, ProductNameSnapshot = "PASS", PriceSnapshot = 100000, SellerNameSnapshot = passSeller.FullName });
    db.Orders.AddRange(shopOrder, passOrder); await db.SaveChangesAsync(); product.Status = "LOCKED"; product.ReservedOrderId = shopOrder.Id;
    db.Add(new PassOrderLock { PostId = post.Id, OrderId = passOrder.Id }); await db.SaveChangesAsync();
    var shopId = shopOrder.Id; var passId = passOrder.Id;
    async Task<DeliveryIssue?> Issue(int id) { db.ChangeTracker.Clear(); return await db.DeliveryIssues.AsNoTracking().SingleOrDefaultAsync(i => i.OrderId == id); }
    async Task Change(int id, int actor, string action, string? note = null)
    {
        var issue = await Issue(id); var version = issue == null ? Convert.ToBase64String((await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id)).RowVersion) : Convert.ToBase64String(issue.RowVersion);
        await new DeliveryIssueService(db).Change(id, actor, action, new DeliveryIssueInput { Version = version, Note = note });
    }
    await Reject<UnauthorizedAccessException>(() => Change(shopId, buyer.Id, "fail", "không liên lạc được"), "only seller reports");
    await Change(shopId, seller.Id, "fail", "Không liên lạc được người nhận");
    Check((await Issue(shopId))?.Status == "FAILED", "failure recorded");
    var staleVersion = Convert.ToBase64String((await Issue(shopId))!.RowVersion);
    Check(await db.Notifications.AnyAsync(n => n.UserId == buyer.Id && n.TargetUrl == $"/orders/{shopId}"), "buyer notified");
    Check((await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id)).Status == "LOCKED", "failure retains shop lock");
    var orders = new OrderService(db, new MockProductLockService(db, NullLogger<MockProductLockService>.Instance), NullLogger<OrderService>.Instance, new ConfigurationBuilder().Build());
    var version = Convert.ToBase64String((await db.Orders.AsNoTracking().SingleAsync(o => o.Id == shopId)).RowVersion);
    await Reject<InvalidOperationException>(() => orders.CollectCodAsync(shopId, seller.Id, new ConfirmOrderRequest { Version = version }), "cannot collect during failure");
    await Change(shopId, seller.Id, "retry"); Check((await Issue(shopId))?.Status == "RETRYING", "retry recorded");
    await Reject<InvalidOperationException>(() => new DeliveryIssueService(db).Change(shopId, seller.Id, "fail", new DeliveryIssueInput { Version = staleVersion, Note = "stale" }), "stale issue version rejected");
    await Change(shopId, seller.Id, "fail", "Lần hai không giao được");
    var issueId = (await Issue(shopId))!.Id;
    Check(await db.DeliveryIssueEvents.CountAsync(e => e.DeliveryIssueId == issueId && e.Action == "fail") == 2, "both failure attempts retained");
    await Change(shopId, seller.Id, "return"); Check((await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id)).Status == "LOCKED", "return transit retains lock");
    await Reject<InvalidOperationException>(() => orders.CollectCodAsync(shopId, seller.Id, new ConfirmOrderRequest { Version = version }), "cannot collect during return");
    await Change(shopId, seller.Id, "receive"); db.ChangeTracker.Clear();
    var returned = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == shopId); var returnedProduct = await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);
    Check(returned.Status == OrderStatus.CodReturned && returned.PaidAt == null, "returned order is distinct and unpaid");
    Check(returnedProduct.Status == "AVAILABLE" && !returnedProduct.IsPublished && returnedProduct.ReservedOrderId == null, "shop item released but not listed");
    await Change(passId, passSeller.Id, "fail", "Sai địa chỉ"); await Change(passId, passSeller.Id, "return");
    Check(await db.Set<PassOrderLock>().AnyAsync(l => l.PostId == post.Id), "PASS remains locked in transit");
    await Change(passId, passSeller.Id, "receive"); db.ChangeTracker.Clear();
    Check(!await db.Set<PassOrderLock>().AnyAsync(l => l.PostId == post.Id), "PASS lock released only after receipt");
    Check((await db.PassPostInfos.AsNoTracking().SingleAsync(p => p.PostId == post.Id)).SaleStatus == "AVAILABLE" && (await db.Posts.AsNoTracking().SingleAsync(p => p.Id == post.Id)).VisibilityStatus == "HIDDEN", "PASS is hidden while preserving unsold status for review");
    Check((await db.Orders.AsNoTracking().SingleAsync(o => o.Id == passId)).Status == OrderStatus.CodReturned, "PASS returned order recorded");
    db.ChangeTracker.Clear(); var retryProduct = new Product { StoreId = shop.Id, CategoryId = category.Id, Title = "Retry", Price = 100000 };
    db.Products.Add(retryProduct); await db.SaveChangesAsync();
    var retryOrder = Fixture(seller.Id, new OrderItem { ProductId = retryProduct.Id, ProductNameSnapshot = "Retry", PriceSnapshot = 100000, StoreIdSnapshot = shop.Id, StoreNameSnapshot = shop.Name, SellerNameSnapshot = seller.FullName });
    db.Orders.Add(retryOrder); await db.SaveChangesAsync(); retryProduct.Status = "LOCKED"; retryProduct.ReservedOrderId = retryOrder.Id; await db.SaveChangesAsync();
    await Change(retryOrder.Id, seller.Id, "fail", "Thử giao lại"); await Change(retryOrder.Id, seller.Id, "retry");
    db.ChangeTracker.Clear(); var retryVersion = Convert.ToBase64String((await db.Orders.AsNoTracking().SingleAsync(o => o.Id == retryOrder.Id)).RowVersion);
    await orders.CollectCodAsync(retryOrder.Id, seller.Id, new ConfirmOrderRequest { Version = retryVersion });
    Check((await Issue(retryOrder.Id))?.Status == "DELIVERED", "successful retry closes delivery issue");
    Check((await db.Products.AsNoTracking().SingleAsync(p => p.Id == retryProduct.Id)).Status == "SOLD", "successful retry marks product sold");
    db.ChangeTracker.Clear(); var raceProduct = new Product { StoreId = shop.Id, CategoryId = category.Id, Title = "Race", Price = 100000 };
    db.Products.Add(raceProduct); await db.SaveChangesAsync();
    var raceOrder = Fixture(seller.Id, new OrderItem { ProductId = raceProduct.Id, ProductNameSnapshot = "Race", PriceSnapshot = 100000, StoreIdSnapshot = shop.Id, StoreNameSnapshot = shop.Name, SellerNameSnapshot = seller.FullName });
    db.Orders.Add(raceOrder); await db.SaveChangesAsync(); raceProduct.Status = "LOCKED"; raceProduct.ReservedOrderId = raceOrder.Id; await db.SaveChangesAsync();
    var raceVersion = Convert.ToBase64String(raceOrder.RowVersion);
    async Task<bool> Race(bool failure)
    {
        await using var worker = new AppDbContext(options);
        try {
            if (failure) await new DeliveryIssueService(worker).Change(raceOrder.Id, seller.Id, "fail", new DeliveryIssueInput { Version = raceVersion, Note = "Không liên lạc được" });
            else await new OrderService(worker, new MockProductLockService(worker, NullLogger<MockProductLockService>.Instance), NullLogger<OrderService>.Instance, new ConfigurationBuilder().Build()).CollectCodAsync(raceOrder.Id, seller.Id, new ConfirmOrderRequest { Version = raceVersion });
            return true;
        } catch (InvalidOperationException) { return false; }
    }
    var race = await Task.WhenAll(Race(true), Race(false));
    Check(race.Count(x => x) == 1, "concurrent failure and COD collection have one winner");
    db.ChangeTracker.Clear(); var raceStatus = await db.Orders.AsNoTracking().Where(o => o.Id == raceOrder.Id).Select(o => o.Status).SingleAsync();
    Check(race[0] ? raceStatus == OrderStatus.CodInTransit && await db.DeliveryIssues.AnyAsync(i => i.OrderId == raceOrder.Id && i.Status == "FAILED") : raceStatus == OrderStatus.CodCompleted && !await db.DeliveryIssues.AnyAsync(i => i.OrderId == raceOrder.Id), "concurrent outcome remains consistent");
    Console.WriteLine($"PASS: {count} delivery failure and return checks.");
}
finally
{
    SqlConnection.ClearAllPools(); const string prefix = "DoosiiDeliveryTest_";
    if (!name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _)) throw new Exception("Unexpected cleanup target");
    await using var cleanup = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]", master); await cleanup.ExecuteNonQueryAsync();
    Console.WriteLine("Removed isolated test database " + name);
}
