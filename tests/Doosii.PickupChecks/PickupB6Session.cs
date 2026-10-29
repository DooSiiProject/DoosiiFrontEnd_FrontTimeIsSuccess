using System.Security.Cryptography;
using System.Text.Json;
using Doosii.API.Configuration;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

// File controls exist only in this test executable, never in the application API.
sealed class PickupB6Session : IDisposable
{
    public string Root { get; } = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".build-part7", "pickup-b6-session"));
    public string Database { get; }
    public byte[] Pepper { get; }
    public byte[] JwtKey { get; }
    public bool Resume { get; }
    public bool Retain { get; set; }
    public long OffsetTicks { get; private set; }
    private readonly ServiceProvider protectionServices;
    public PickupB6Session(bool resume)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("This isolated B6 fixture uses Windows DPAPI. Certificate hosting needs its own deployment smoke.");
        Resume = resume;
        if (Directory.Exists(Root) != resume) throw new InvalidOperationException(resume ? "No B6 session to resume." : "B6 session exists; explicitly resume it before cleanup.");
        // Exercise the same startup guards as production before touching a key directory.
        foreach (var badPath in new string?[] { null, "relative-key-ring" })
        {
            var bad = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Pickup:CodePepper"] = "fixture-configured", ["Pickup:DataProtectionKeyPath"] = badPath }).Build();
            try { new ServiceCollection().AddPickupDataProtection(bad, new ProductionFixtureEnvironment()); throw new Exception("Production accepted unsafe persistence configuration."); }
            catch (InvalidOperationException) { }
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Pickup:CodePepper"] = "fixture-configured", ["Pickup:DataProtectionKeyPath"] = Path.Combine(Root, "keys") }).Build();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddPickupDataProtection(configuration, new ProductionFixtureEnvironment());
        protectionServices = services.BuildServiceProvider();
        var protector = protectionServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("DooSii.Pickup.B6.FixtureSecrets.v1");
        var manifest = Path.Combine(Root, "session.protected");
        if (resume)
        {
            var restored = JsonSerializer.Deserialize<FixtureSecrets>(protector.Unprotect(File.ReadAllText(manifest)))!;
            Database = restored.Database; Pepper = Convert.FromBase64String(restored.Pepper); JwtKey = Convert.FromBase64String(restored.JwtKey);
            OffsetTicks = long.Parse(File.ReadAllText(Path.Combine(Root, "offset.txt")), System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            Database = "DoosiiPickupHttpTest_" + Guid.NewGuid().ToString("N");
            Pepper = RandomNumberGenerator.GetBytes(32); JwtKey = RandomNumberGenerator.GetBytes(48);
            File.WriteAllText(manifest, protector.Protect(JsonSerializer.Serialize(new FixtureSecrets(Database, Convert.ToBase64String(Pepper), Convert.ToBase64String(JwtKey)))));
            File.WriteAllText(Path.Combine(Root, "offset.txt"), "0");
        }
        if (!Database.StartsWith("DoosiiPickupHttpTest_", StringComparison.Ordinal) || Database.Length != 53 ||
            !Guid.TryParseExact(Database[21..], "N", out _)) throw new InvalidOperationException("Unexpected B6 database target.");
        // Assert the persisted key material itself is encrypted by DPAPI.
        var keyFiles = Directory.GetFiles(Path.Combine(Root, "keys"), "key-*.xml");
        if (keyFiles.Length == 0 || keyFiles.Any(path => !File.ReadAllText(path).Contains("encryptedSecret")))
            throw new Exception("Fixture key ring was not encrypted at rest.");
    }
    public void Configure(WebApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Pickup:CodePepper"] = Convert.ToBase64String(Pepper), ["Pickup:DataProtectionKeyPath"] = Path.Combine(Root, "keys") });
        builder.Services.AddPickupDataProtection(builder.Configuration, builder.Environment);
    }
    public async Task ProcessCommand(WebApplication app, FixtureClock clock, User[] users, int shopId)
    {
        var commandPath = Path.Combine(Root, "command.json");
        if (!File.Exists(commandPath)) return;
        var command = JsonDocument.Parse(await File.ReadAllTextAsync(commandPath)).RootElement;
        var id = command.GetProperty("id").GetString();
        var action = command.GetProperty("action").GetString();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (action == "advance")
        {
            var seconds = command.GetProperty("seconds").GetInt32();
            if (seconds < 0 || seconds > 86400) throw new InvalidOperationException("Fixture clock advance outside test bounds.");
            clock.Advance(TimeSpan.FromSeconds(seconds)); OffsetTicks = clock.OffsetTicks;
            await File.WriteAllTextAsync(Path.Combine(Root, "offset.txt"), OffsetTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        else if (action == "relocate")
        {
            var relocations = scope.ServiceProvider.GetRequiredService<IShopRelocationService>();
            var change = await relocations.CreateAsync(users[20].Id, shopId, new CreateShopRelocationRequest {
                ClientRequestId = Guid.NewGuid(), ProposedAddress = "QA B6 new pickup address", ProposedLatitude = 10.78, ProposedLongitude = 106.71, Reason = "Synthetic B6 relocation" });
            await relocations.ReviewAsync(users[22].Id, change.Id, new ReviewShopRelocationRequest {
                ClientRequestId = Guid.NewGuid(), Decision = "APPROVE", Reason = "Synthetic B6 admin review" });
        }
        else if (action != "report") throw new InvalidOperationException("Unknown B6 fixture command.");
        var result = new { id, action, serverTime = clock.GetUtcNow(),
            reservations = await db.PickupReservations.AsNoTracking().Select(r => new { r.Id, r.ProductId, r.Status, r.CloseReason }).ToArrayAsync(),
            products = await db.Products.AsNoTracking().Select(p => new { p.Id, p.Status, p.ReservedPickupReservationId, p.ReservedOrderId }).ToArrayAsync(),
            orders = await db.Orders.CountAsync(), escrows = await db.EscrowTransactions.CountAsync(),
            notifications = await db.Notifications.CountAsync(), outbox = await db.PickupNotificationOutbox.CountAsync() };
        var resultPath = Path.Combine(Root, "result.json");
        await File.WriteAllTextAsync(resultPath + ".tmp", JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        File.Move(resultPath + ".tmp", resultPath, true);
        File.Delete(commandPath);
    }
    public void Dispose()
    {
        protectionServices.Dispose(); CryptographicOperations.ZeroMemory(Pepper); CryptographicOperations.ZeroMemory(JwtKey);
        if (!Retain)
        {
            var expected = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".build-part7", "pickup-b6-session"));
            if (!string.Equals(Root, expected, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Root)) throw new InvalidOperationException("Unexpected key cleanup target.");
            Directory.Delete(Root, true);
        }
    }
    private sealed record FixtureSecrets(string Database, string Pepper, string JwtKey);
    private sealed class ProductionFixtureEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "DooSii";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
