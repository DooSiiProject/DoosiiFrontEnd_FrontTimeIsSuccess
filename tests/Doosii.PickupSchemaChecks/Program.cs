using System.ComponentModel.DataAnnotations;
using Doosii.BLL.DTOs;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

// Only a newly named disposable DB on localhost; never reads application configuration.
var name = "DoosiiPickupTest_" + Guid.NewGuid().ToString("N");
const string masterConnection = "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;Encrypt=false";
await using var master = new SqlConnection(masterConnection);
await master.OpenAsync();
await using (var create = new SqlCommand($"CREATE DATABASE [{name}]", master)) await create.ExecuteNonQueryAsync();
var connection = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = name }.ConnectionString;
var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
int checks = 0;
try
{
    await using var db = new AppDbContext(options);
    await db.Database.MigrateAsync();
    Assert(!db.Database.HasPendingModelChanges(), "model matches snapshot");
    var people = Enumerable.Range(0, 4).Select(i => new User { Email = $"pickup{i}@example.invalid", FullName = "QA", PasswordHash = "not-a-login", Role = i == 3 ? "Shopper" : "CasualUser" }).ToArray();
    db.Users.AddRange(people); await db.SaveChangesAsync();
    var shop = new Store { OwnerId = people[3].Id, Name = "Fixture shop", Address = "Fixture address" };
    var otherShop = new Store { OwnerId = people[3].Id, Name = "Other fixture shop" };
    // One shop per owner may be enforced by the existing model, so use a different owner.
    otherShop.OwnerId = people[2].Id;
    var category = new Category { Name = "QA", Slug = "pickup-qa" };
    db.Stores.AddRange(shop, otherShop); db.Categories.Add(category); await db.SaveChangesAsync();
    var products = Enumerable.Range(0, 3).Select(i => new Product { StoreId = shop.Id, CategoryId = category.Id, Title = $"QA {i}", Price = 250000 }).ToArray();
    db.Products.AddRange(products); await db.SaveChangesAsync();
    Assert(products.All(p => p.Id > 0), "product identity preserved");
    var now = DateTime.UtcNow;
    PickupReservation New(int productIndex, int buyerIndex, bool accepted = false) => new()
    {
        BuyerId = people[buyerIndex].Id, SellerId = people[3].Id, StoreId = shop.Id,
        ProductId = products[productIndex].Id, ClientRequestId = Guid.NewGuid(), RequestHash = new string('a', 64),
        ContactName = "QA", ContactPhone = "0900000000", ProductTitleSnapshot = "QA product", PriceSnapshot = 250000,
        ProductVersionSnapshot = products[productIndex].RowVersion, CreatedAt = now,
        ScheduledAt = now.AddHours(3), ResponseDeadline = now.AddHours(2),
        Status = accepted ? "ACCEPTED" : "PENDING", AcceptedAt = accepted ? now.AddMinutes(1) : null,
        ExpiresAt = accepted ? now.AddHours(3.5) : null,
        StoreNameSnapshot = accepted ? "Fixture shop" : null, StoreAddressSnapshot = accepted ? "Fixture address" : null,
        PickupCodeHash = accepted ? new byte[32] : null, ProtectedPickupCode = accepted ? "synthetic-ciphertext" : null
    };
    var pending = New(0, 0); var accepted = New(1, 1, true); var completed = New(2, 2, true);
    completed.Status = "COMPLETED"; completed.ClosedAt = now.AddHours(3); completed.ClosedById = people[3].Id; completed.CloseReason = "PAYMENT_RECEIVED_AT_STORE";
    db.PickupReservations.AddRange(pending, accepted, completed); await db.SaveChangesAsync();
    Assert(pending.RowVersion.Length == 8 && accepted.RowVersion.Length == 8, "SQL rowversions generated");
    products[1].Status = "LOCKED"; products[1].ReservedPickupReservationId = accepted.Id;
    products[2].Status = "SOLD"; await db.SaveChangesAsync();
    Assert(await db.PickupReservations.CountAsync() == 3, "valid pending, accepted and completed fixtures");

    async Task Reject(string sql, string constraint, params object[] args)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try { await db.Database.ExecuteSqlRawAsync(sql, args); throw new Exception("Accepted invalid write: " + constraint); }
        catch (SqlException error) when (error.Number is 547 or 2601 or 2627 && error.Message.Contains(constraint, StringComparison.Ordinal)) { checks++; }
        finally { await tx.RollbackAsync(); }
    }
    async Task RejectInsert(PickupReservation row, string constraint)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try { db.PickupReservations.Add(row); await db.SaveChangesAsync(); throw new Exception("Accepted invalid insert: " + constraint); }
        catch (DbUpdateException error) when (error.InnerException is SqlException sql && sql.Number is 547 or 2601 or 2627 && sql.Message.Contains(constraint, StringComparison.Ordinal)) { checks++; }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
    }
    await Reject("UPDATE store.PickupReservations SET Status='UNKNOWN' WHERE Id={0}", "CK_Pickup", pending.Id);
    await Reject("UPDATE store.PickupReservations SET PriceSnapshot=-1 WHERE Id={0}", "CK_Pickup_TextPrice", pending.Id);
    await Reject("UPDATE store.PickupReservations SET ContactName=' ' WHERE Id={0}", "CK_Pickup_TextPrice", pending.Id);
    await Reject("UPDATE store.PickupReservations SET BuyerId=SellerId WHERE Id={0}", "CK_Pickup_BuyerSeller", pending.Id);
    await Reject("UPDATE store.PickupReservations SET ScheduledAt=DATEADD(minute,29,CreatedAt) WHERE Id={0}", "CK_Pickup_Schedule", pending.Id);
    await Reject("UPDATE store.PickupReservations SET ScheduledAt=DATEADD(hour,25,CreatedAt) WHERE Id={0}", "CK_Pickup_Schedule", pending.Id);
    await Reject("UPDATE store.PickupReservations SET ResponseDeadline=DATEADD(hour,3,CreatedAt) WHERE Id={0}", "CK_Pickup_Schedule", pending.Id);
    await Reject("UPDATE store.PickupReservations SET StoreId={0} WHERE Id={1}", "FK_PickupReservations_Products", otherShop.Id, pending.Id);
    await Reject("UPDATE store.PickupReservations SET Status='ACCEPTED' WHERE Id={0}", "CK_Pickup_Acceptance", pending.Id);
    await Reject("UPDATE store.PickupReservations SET ExpiresAt=NULL WHERE Id={0}", "CK_Pickup_Acceptance", accepted.Id);
    await Reject("UPDATE store.PickupReservations SET StoreAddressSnapshot='' WHERE Id={0}", "CK_Pickup_Acceptance", accepted.Id);
    await Reject("UPDATE store.PickupReservations SET FailedCodeAttempts=6 WHERE Id={0}", "CK_Pickup_CodeAttempts", accepted.Id);
    await Reject("UPDATE store.PickupReservations SET StoreLatitudeSnapshot=91,StoreLongitudeSnapshot=106 WHERE Id={0}", "CK_Pickup_Coordinates", accepted.Id);
    await Reject("UPDATE store.PickupReservations SET ClientRequestId='00000000-0000-0000-0000-000000000000' WHERE Id={0}", "CK_Pickup_Request", pending.Id);
    await Reject("UPDATE store.PickupReservations SET Status='CANCELLED',ClosedAt=CreatedAt,CloseReason='cancel',ClosedById=NULL WHERE Id={0}", "CK_Pickup_Closure", pending.Id);
    await Reject("UPDATE store.PickupReservations SET ClosedAt=ExpiresAt WHERE Id={0}", "CK_Pickup_Closure", completed.Id);
    await Reject("UPDATE store.Products SET ReservedOrderId=99 WHERE Id={0}", "CK_Products_ReservationOwner", products[1].Id);
    await Reject("UPDATE store.Products SET Status='AVAILABLE' WHERE Id={0}", "CK_Products_ReservationOwner", products[1].Id);
    await Reject("UPDATE store.Products SET Status='LOCKED',ReservedPickupReservationId={0} WHERE Id={1}", "FK_Products_PickupReservations", accepted.Id, products[0].Id);
    await RejectInsert(New(0, 0), "IX_PickupReservations_BuyerId_ProductId");
    await RejectInsert(New(1, 0, true), "IX_PickupReservations_ProductId");
    var duplicate = New(2, 0); duplicate.ClientRequestId = pending.ClientRequestId;
    await RejectInsert(duplicate, "IX_PickupReservations_BuyerId_ClientRequestId");
    var invalidHash = New(2, 0); invalidHash.RequestHash = new string('z', 64);
    await RejectInsert(invalidHash, "CK_Pickup_Request");
    var invalidVersion = New(2, 0); invalidVersion.ProductVersionSnapshot = new byte[7];
    await RejectInsert(invalidVersion, "CK_Pickup_Request");
    // FK also protects reservation history from product deletion.
    await Reject("DELETE FROM store.Products WHERE Id={0}", "FK_PickupReservations_Products", products[0].Id);

    var validation = new List<ValidationResult>();
    var invalidCreate = new CreatePickupReservationRequest();
    Assert(!Validator.TryValidateObject(invalidCreate, new ValidationContext(invalidCreate), validation, true), "invalid create DTO rejected");
    validation.Clear(); var completeDto = new CompletePickupReservationRequest { Version = "AAAAAAAAAAE=", PickupCode = "123456" };
    Assert(!Validator.TryValidateObject(completeDto, new ValidationContext(completeDto), validation, true), "payment confirmation mandatory");
    Assert(typeof(PickupReservationDto).GetProperty("PickupCode") == null && typeof(PickupReservationDto).GetProperty("PickupCodeHash") == null, "private code excluded from ordinary response");

    var migrator = db.GetService<IMigrator>();
    try { await migrator.MigrateAsync("20261027111239_SupportCommunityPublishing"); throw new Exception("Down discarded pickup history"); }
    catch (SqlException error) when (error.Number == 51009) { checks++; }
    await db.Products.Where(p => p.ReservedPickupReservationId != null).ExecuteUpdateAsync(s => s.SetProperty(p => p.ReservedPickupReservationId, (int?)null).SetProperty(p => p.Status, "AVAILABLE"));
    await db.PickupReservations.ExecuteDeleteAsync();
    await migrator.MigrateAsync("20261027111239_SupportCommunityPublishing");
    await migrator.MigrateAsync();
    Assert(await db.Products.CountAsync() == 3, "Down/Up preserves existing products");
    Assert(!await db.PickupReservations.AnyAsync(), "empty pickup schema after Down/Up");
    Console.WriteLine($"PASS: {checks} SQL/model/DTO assertions; actual migrations Up/Down, no application database touched.");
    void Assert(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
}
finally
{
    SqlConnection.ClearAllPools();
    if (!name.StartsWith("DoosiiPickupTest_", StringComparison.Ordinal) || name.Length != 49) throw new Exception("Unexpected cleanup target");
    await using var cleanup = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];", master);
    await cleanup.ExecuteNonQueryAsync();
    Console.WriteLine("Removed disposable database " + name);
}
