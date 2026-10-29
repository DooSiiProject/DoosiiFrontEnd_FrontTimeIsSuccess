using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;
using Doosii.DAL.Models.Forum;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Doosii.BLL.Services;
using Doosii.BLL.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;

const string masterConnection = "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;Encrypt=false";
var name = "DoosiiOrderSourcesTest_" + Guid.NewGuid().ToString("N");
await using var master = new SqlConnection(masterConnection); await master.OpenAsync();
await using (var create = new SqlCommand($"CREATE DATABASE [{name}]; ALTER DATABASE [{name}] SET QUERY_STORE=OFF;", master)) await create.ExecuteNonQueryAsync();
var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = name }.ConnectionString).Options;
int checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
try
{
    await using var db = new AppDbContext(options); await db.Database.MigrateAsync();
    var migration = db.GetService<IMigrator>();
    const string previous = "20261028022627_AddPickupNotificationOutbox";
    var buyer = new User { Email = "source-buyer@example.invalid", FullName = "Buyer", PasswordHash = "fixture" };
    var seller = new User { Email = "source-seller@example.invalid", FullName = "Original seller", PasswordHash = "fixture", Role = "Shopper" };
    db.Users.AddRange(buyer, seller); await db.SaveChangesAsync();
    var shop = new Store { OwnerId = seller.Id, Name = "Original shop" };
    var category = new Category { Name = "Fixture", Slug = "source-fixture" };
    db.AddRange(shop, category); await db.SaveChangesAsync();
    var product = new Product { StoreId = shop.Id, CategoryId = category.Id, Title = "Current product", Price = 200000 };
    db.Products.Add(product); await db.SaveChangesAsync();
    var order = new Order { BuyerId = buyer.Id, SellerId = seller.Id, ReceiverName = "Receiver", ReceiverPhone = "0900000000", ShippingAddress = "Fixture address", ProductPrice = 150000, TotalAmount = 150000,
        Items = new List<OrderItem> { new() { ProductId = product.Id, ProductNameSnapshot = "Historic item", PriceSnapshot = 150000, ThumbnailSnapshot = "https://example.invalid/old.jpg" } } };
    db.Orders.Add(order); await db.SaveChangesAsync(); var itemId = order.Items.Single().Id;
    // Make an actual populated pre-migration schema, then upgrade it with old orders.
    await migration.MigrateAsync(previous); await migration.MigrateAsync(); db.ChangeTracker.Clear();
    var item = await db.OrderItems.SingleAsync(i => i.Id == itemId);
    Check(item.ProductId == product.Id && item.SourceType == "SHOP_PRODUCT" && item.PassPostId == null, "legacy source and ID preserved");
    Check(item.ProductNameSnapshot == "Historic item" && item.PriceSnapshot == 150000 && item.ThumbnailSnapshot == "https://example.invalid/old.jpg", "old product snapshots not overwritten");
    Check(item.StoreNameSnapshot == "Original shop" && item.SellerNameSnapshot == "Original seller" && item.SnapshotOrigin == "LEGACY_BACKFILL", "legacy backfill is explicitly labelled");
    await db.Stores.Where(s => s.Id == shop.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, "Renamed shop"));
    Check((await db.OrderItems.AsNoTracking().SingleAsync(i => i.Id == itemId)).StoreNameSnapshot == "Original shop", "snapshot survives shop rename");
    var post = new Post { AuthorId = buyer.Id, Type = "PASS", Title = "PASS fixture", PostDescription = "Description", ClientRequestId = Guid.NewGuid(),
        PassInfo = new PassPostInfo { ProductName = "Pass item", Price = 100000, Size = "M", Defects = "Không có" } };
    db.Posts.Add(post); await db.SaveChangesAsync();
    async Task Reject(string sql, string label)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try { await db.Database.ExecuteSqlRawAsync(sql); throw new Exception("Invalid data accepted: " + label); }
        catch (SqlException error) when (error.Number == 547) { checks++; }
        finally { await tx.RollbackAsync(); }
    }
    await Reject($"UPDATE dbo.OrderItems SET PassPostId={post.Id} WHERE Id={itemId}", "both sources");
    await Reject($"UPDATE dbo.OrderItems SET ProductId=NULL WHERE Id={itemId}", "no source");
    await Reject($"UPDATE dbo.OrderItems SET SourceType='OTHER' WHERE Id={itemId}", "unknown source");
    await Reject($"UPDATE dbo.OrderItems SET ProductId=2147483647 WHERE Id={itemId}", "missing product FK");
    await Reject($"UPDATE dbo.OrderItems SET StoreIdSnapshot=NULL WHERE Id={itemId}", "partial shop snapshot");
    var passItem = new OrderItem { OrderId = order.Id, SourceType = "PASS_POST", PassPostId = post.Id, ProductNameSnapshot = "Pass snapshot", PriceSnapshot = 100000, SellerNameSnapshot = "Pass seller" };
    db.OrderItems.Add(passItem); await db.SaveChangesAsync();
    Check(passItem.Id > 0 && passItem.ProductId == null, "valid PASS source persisted");
    var passLock = new PassOrderLock { PostId = post.Id, OrderId = order.Id };
    db.Set<PassOrderLock>().Add(passLock); await db.SaveChangesAsync();
    try { await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO forum.PassOrderLocks(PostId,OrderId,CreatedAt) VALUES ({post.Id},{order.Id},{DateTime.UtcNow})"); throw new Exception("duplicate PASS lock accepted"); }
    catch (SqlException error) when (error.Number is 2601 or 2627) { checks++; }
    try { await migration.MigrateAsync("20261029162005_AddOrderItemSources"); throw new Exception("active locks discarded on rollback"); }
    catch (SqlException error) when (error.Number == 51022) { checks++; }
    Check(await db.Set<PassOrderLock>().AnyAsync(x => x.PostId == post.Id), "rollback preserves active lock");
    db.Set<PassOrderLock>().Remove(passLock); await db.SaveChangesAsync();
    await Reject($"UPDATE dbo.OrderItems SET PassPostId=2147483647 WHERE Id={passItem.Id}", "missing PASS FK");
    await Reject($"UPDATE dbo.OrderItems SET PassPostId={product.Id + 10000} WHERE Id={passItem.Id}", "non-PASS reference");
    try { passItem.RequireShopProductId(); throw new Exception("PASS passed as shop product"); } catch (InvalidOperationException) { checks++; }
    try { await migration.MigrateAsync(previous); throw new Exception("Rollback lost PASS data"); }
    catch (SqlException error) when (error.Number == 51021) { checks++; }
    Check(await db.OrderItems.AnyAsync(i => i.Id == passItem.Id), "guard preserves PASS item");
    db.OrderItems.Remove(passItem); await db.SaveChangesAsync();
    // Invalid legacy references must abort upgrade, not silently drop or fabricate rows.
    await migration.MigrateAsync(previous);
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.OrderItems SET ProductId=2147483647 WHERE Id={itemId}");
    try { await migration.MigrateAsync(); throw new Exception("Orphan legacy reference accepted"); }
    catch (SqlException error) when (error.Number == 51020) { checks++; }
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.OrderItems SET ProductId={product.Id} WHERE Id={itemId}");
    await migration.MigrateAsync();
    Check(!db.Database.HasPendingModelChanges(), "EF model matches snapshot");
    Check(await db.Orders.CountAsync() == 1 && await db.OrderItems.CountAsync() == 1, "upgrade and downgrade preserve old orders");
    db.ChangeTracker.Clear();
    db.MerchantProfiles.Add(new MerchantProfile { UserId = seller.Id, KycStatus = "APPROVED" }); await db.SaveChangesAsync();
    var service = new OrderService(db, new MockProductLockService(db, NullLogger<MockProductLockService>.Instance), NullLogger<OrderService>.Instance, new ConfigurationBuilder().Build());
    var quote = await service.QuoteAsync(buyer.Id, product.Id);
    var request = new CreateEscrowOrderRequest { ProductId = product.Id, ProductVersion = quote.ProductVersion, ClientRequestId = Guid.NewGuid(), PaymentMethod = "COD", ReceiverName = "Receiver", ReceiverPhone = "0900000000", ShippingAddress = "Fixture address" };
    var created = await service.CreateEscrowOrderAsync(buyer.Id, request);
    Check(created.StoreId == shop.Id && created.StoreName == "Renamed shop", "new order API resolves snapshot shop");
    Check(created.Items.Single().SnapshotOrigin == "AT_ORDER" && created.Items.Single().SellerNameSnapshot == "Original seller", "new order snapshots recorded at creation");
    var saved = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == created.Id);
    Check(saved.SourceVersionSnapshot?.Length == 8 && saved.SourceType == "SHOP_PRODUCT", "new order source version captured");
    await db.Stores.Where(s => s.Id == shop.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, "Renamed again"));
    db.ChangeTracker.Clear();
    Check((await service.GetOrderByIdAsync(created.Id, buyer.Id)).StoreName == "Renamed shop", "detail does not replace historic shop name");
    Check((await service.GetMyPurchasesAsync(buyer.Id)).Single(o => o.Id == created.Id).StoreName == "Renamed shop", "purchase list uses same snapshot");
    Check((await service.GetMySalesAsync(seller.Id)).Single(o => o.Id == created.Id).StoreName == "Renamed shop", "seller list uses same snapshot");
    Check((await service.CreateEscrowOrderAsync(buyer.Id, request)).Id == created.Id, "shop retry remains idempotent");
    var cancelled = await service.CancelAsync(created.Id, seller.Id, new ConfirmOrderRequest { Version = created.Version });
    Check(cancelled.Status == OrderStatus.Cancelled, "shop seller may cancel COD before shipping and release product");
    var passSeller = new User { Email = "pass-seller@example.invalid", FullName = "Pass seller", PasswordHash = "fixture", Role = "CasualUser" };
    var buyerTwo = new User { Email = "pass-buyer2@example.invalid", FullName = "Buyer two", PasswordHash = "fixture", Role = "CasualUser" };
    db.Users.AddRange(passSeller, buyerTwo); await db.SaveChangesAsync();
    var codPost = new Post { AuthorId = passSeller.Id, Type = "PASS", Title = "COD PASS", PostDescription = "Fixture", ClientRequestId = Guid.NewGuid(),
        PassInfo = new PassPostInfo { ProductName = "Vintage", Price = 100000, Size = "M", Defects = "Không có" } };
    db.Posts.Add(codPost); await db.SaveChangesAsync();
    var passQuote = await service.QuotePassCodAsync(buyer.Id, codPost.Id);
    Check(passQuote.ProductPrice == 100000 && passQuote.EscrowFee == 0 && passQuote.PassPostId == codPost.Id, "PASS COD quote uses seller and no escrow");
    CreatePassCodRequest PassRequest() => new() { PassPostId = codPost.Id, ProductVersion = passQuote.ProductVersion, ClientRequestId = Guid.NewGuid(), ReceiverName = "Buyer", ReceiverPhone = "0900000000", ShippingAddress = "Fixture" };
    var requestA = PassRequest(); var requestB = PassRequest();
    async Task<OrderResponse?> Race(int userId, CreatePassCodRequest body)
    {
        await using var context = new AppDbContext(options);
        var worker = new OrderService(context, new MockProductLockService(context, NullLogger<MockProductLockService>.Instance), NullLogger<OrderService>.Instance, new ConfigurationBuilder().Build());
        try { return await worker.CreatePassCodAsync(userId, body); } catch (InvalidOperationException) { return null; }
    }
    var race = await Task.WhenAll(Race(buyer.Id, requestA), Race(buyerTwo.Id, requestB));
    Check(race.Count(x => x != null) == 1, "concurrent PASS orders have one winner");
    var winner = race.Single(x => x != null)!; var winnerId = winner.BuyerId;
    Check(await db.Set<PassOrderLock>().CountAsync(l => l.PostId == codPost.Id && l.OrderId == winner.Id) == 1, "lock owned by winner");
    db.ChangeTracker.Clear(); var closedPass = await service.CancelAsync(winner.Id, winnerId, new ConfirmOrderRequest { Version = winner.Version });
    Check(closedPass.Status == OrderStatus.Cancelled && !await db.Set<PassOrderLock>().AnyAsync(l => l.PostId == codPost.Id), "cancel releases PASS lock");
    db.ChangeTracker.Clear(); var secondQuote = await service.QuotePassCodAsync(buyer.Id, codPost.Id); var second = PassRequest(); second.ProductVersion = secondQuote.ProductVersion;
    var passOrder = await service.CreatePassCodAsync(buyer.Id, second);
    Check((await service.CreatePassCodAsync(buyer.Id, second)).Id == passOrder.Id, "PASS COD retry does not duplicate order");
    var shipped = await service.ShipOrderAsync(passOrder.Id, passSeller.Id, new ShipOrderRequest { Version = passOrder.Version, ShippingCarrier = "Fixture ship", TrackingCode = "TEST123" });
    var complete = await service.CollectCodAsync(passOrder.Id, passSeller.Id, new ConfirmOrderRequest { Version = shipped.Version });
    db.ChangeTracker.Clear();
    Check(complete.Status == OrderStatus.CodCompleted && (await db.Set<PassPostInfo>().SingleAsync(p => p.PostId == codPost.Id)).SaleStatus == "SOLD", "PASS COD completion marks sold");
    Check(!await db.Set<PassOrderLock>().AnyAsync(l => l.PostId == codPost.Id), "completion releases lock");
    Check(!await db.EscrowTransactions.AnyAsync(e => e.OrderId == passOrder.Id), "PASS COD never creates escrow");
    Console.WriteLine($"PASS: {checks} SQL migration/source/snapshot/rollback assertions.");
}
finally
{
    SqlConnection.ClearAllPools();
    const string prefix = "DoosiiOrderSourcesTest_";
    if (!name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _)) throw new Exception("Unexpected cleanup target");
    await using var cleanup = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];", master);
    await cleanup.ExecuteNonQueryAsync(); Console.WriteLine("Removed disposable database " + name);
}
