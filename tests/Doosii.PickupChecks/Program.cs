using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Doosii.API.Controllers;
using Doosii.API.BackgroundServices;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.BLL.Hubs;
using Doosii.BLL.Services;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Store;
using Doosii.DAL.Models.Forum;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

// Actual SQL + controllers/JWT/transactions. No production Program, PayOS or Cloudinary.
var uiMode = args.Contains("--ui");
if (args.Contains("--b6") && !uiMode) throw new InvalidOperationException("B6 persistence is only supported in the isolated UI host.");
using var b6 = args.Contains("--b6") ? new PickupB6Session(args.Contains("--resume")) : null;
var database = b6?.Database ?? "DoosiiPickupHttpTest_" + Guid.NewGuid().ToString("N");
const string masterConnection = "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;Encrypt=false";
await using var master = new SqlConnection(masterConnection); await master.OpenAsync();
if (b6?.Resume != true)
{
    // Disposable databases need no Query Store history; avoid teardown flush failures.
    await using var create = new SqlCommand($"CREATE DATABASE [{database}]; ALTER DATABASE [{database}] SET QUERY_STORE = OFF;", master);
    await create.ExecuteNonQueryAsync();
}
var connection = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = database }.ConnectionString;
WebApplication? app = null;
int checks = 0;
var time = new FixtureClock(uiMode);
if (b6 != null) time.Advance(TimeSpan.FromTicks(b6.OffsetTicks));
var pepper = b6?.Pepper ?? RandomNumberGenerator.GetBytes(32);
var provider = new EphemeralDataProtectionProvider();
var hub = new RecordingHub();
try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = b6 != null ? "Production" : "Development" }); builder.WebHost.UseUrls(uiMode ? "http://127.0.0.1:5258" : "http://127.0.0.1:0");
    builder.Logging.ClearProviders(); builder.Logging.AddConsole(); builder.Logging.SetMinimumLevel(LogLevel.Error);
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Pickup:CodePepper"] = Convert.ToBase64String(pepper) });
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connection));
    builder.Services.AddSingleton<TimeProvider>(time);
    if (b6 != null) b6.Configure(builder); else builder.Services.AddSingleton<IDataProtectionProvider>(provider);
    builder.Services.AddSingleton<PickupCodeProtection>(); builder.Services.AddScoped<IPickupReservationService, PickupReservationService>();
    builder.Services.AddScoped<IOrderService, OrderService>(); builder.Services.AddScoped<IProductLockService, MockProductLockService>();
    builder.Services.AddScoped<IProductService, ProductService>();
    builder.Services.AddScoped<IProductGroupService, ProductGroupService>();
    builder.Services.AddSignalR();
    if (!uiMode) builder.Services.AddSingleton<IHubContext<NotificationHub>>(hub);
    if (uiMode) builder.Services.AddHostedService<PickupMaintenanceWorker>();
    builder.Services.AddScoped<INotificationService, NotificationService>(); builder.Services.AddScoped<IStoreService, StoreService>();
    builder.Services.AddScoped<PickupMaintenanceService>(); builder.Services.AddScoped<PickupNotificationDelivery>();
    builder.Services.AddScoped<IShopRelocationService, ShopRelocationService>();
    builder.Services.AddControllers().AddApplicationPart(typeof(PickupReservationsController).Assembly)
        .ConfigureApplicationPartManager(m => m.FeatureProviders.Add(new FixtureControllers()));
    var key = new SymmetricSecurityKey(b6?.JwtKey ?? RandomNumberGenerator.GetBytes(48));
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new() {
        ValidateIssuer = true, ValidIssuer = "pickup-test", ValidateAudience = true, ValidAudience = "pickup-test", ValidateIssuerSigningKey = true,
        IssuerSigningKey = key, ValidateLifetime = true, ClockSkew = TimeSpan.Zero });
    builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => options.Events = new JwtBearerEvents {
        OnMessageReceived = context => { if (context.Request.Path.StartsWithSegments("/hubs")) context.Token = context.Request.Query["access_token"]; return Task.CompletedTask; }
    });
    builder.Services.AddAuthorization();
    app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
    User[] users; Product[] products; int shopId;
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.MigrateAsync();
        if (b6?.Resume == true)
        {
            users = await db.Users.Where(u => u.Email.EndsWith("@example.invalid")).OrderBy(u => u.Id).ToArrayAsync();
            products = await db.Products.OrderBy(p => p.Id).ToArrayAsync();
            shopId = await db.Stores.Where(s => s.OwnerId == users[20].Id).Select(s => s.Id).SingleAsync();
            if (users.Length != 24 || products.Length != 40) throw new InvalidOperationException("B6 fixture state is incomplete.");
        }
        else
        {
        users = Enumerable.Range(0, 24).Select(i => new User { Email = $"pickup{i}@example.invalid", FullName = $"QA {i}", PasswordHash = "not-a-login", Role = i switch { 20 or 21 => "Shopper", 22 => "Admin", 23 => "Banned", _ => "CasualUser" } }).ToArray();
        db.Users.AddRange(users); await db.SaveChangesAsync();
        db.MerchantProfiles.AddRange(new MerchantProfile { UserId = users[20].Id, KycStatus = "APPROVED" }, new MerchantProfile { UserId = users[21].Id, KycStatus = "APPROVED" });
        var shop = new Store { OwnerId = users[20].Id, Name = "QA Shop", Address = "QA pickup address", Latitude = 10.77, Longitude = 106.70 };
        var other = new Store { OwnerId = users[21].Id, Name = "QA Other", Address = "QA other address" };
        var category = new Category { Name = "QA", Slug = "pickup-test" };
        db.Stores.AddRange(shop, other); db.Categories.Add(category); await db.SaveChangesAsync(); shopId = shop.Id;
        products = Enumerable.Range(0, 40).Select(i => new Product { StoreId = i == 39 ? other.Id : shop.Id, CategoryId = category.Id, Title = $"QA product {i}", Price = 250000 }).ToArray();
        db.Products.AddRange(products); await db.SaveChangesAsync();
        }
    }
    if (uiMode)
    {
        app.MapPost("/api/Auth/login", async (LoginRequest request, AppDbContext db) => {
            var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == request.Email);
            if (actor == null || request.Password != "pickup-ui-test") return Results.Unauthorized();
            var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("pickup-test", "pickup-test", new[] {
                new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim(ClaimTypes.Role, actor.Role) }, expires: DateTime.UtcNow.AddHours(2), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
            return Results.Ok(ApiResponse.Ok(new AuthResponse { Token = token, RefreshToken = "fixture-only", User = new UserDto { Id = actor.Id, Email = actor.Email, FullName = actor.FullName, Role = actor.Role } }));
        });
        app.MapGet("/api/Auth/me", async (ClaimsPrincipal principal, AppDbContext db) => {
            var id = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!); var actor = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
            return Results.Ok(ApiResponse.Ok(new UserDto { Id = actor.Id, Email = actor.Email, FullName = actor.FullName, Role = actor.Role }));
        }).RequireAuthorization();
        app.MapHub<NotificationHub>("/hubs/notifications"); app.MapHub<PickupFixtureChatHub>("/hubs/chat");
    }
    await app.StartAsync();
    if (uiMode)
    {
        var stopFile = System.IO.Path.Combine(Directory.GetCurrentDirectory(), ".build-part7", "pickup-ui.stop");
        if (File.Exists(stopFile)) File.Delete(stopFile);
        Console.WriteLine("Pickup UI fixture: http://127.0.0.1:5258; pickup0/pickup1 CasualUser, pickup20/pickup21 Shopper, pickup22 Admin @example.invalid; password pickup-ui-test. Products 1..39 Shop 1, product 40 Shop 2. Create " + stopFile + " to stop and remove disposable DB.");
        while (!File.Exists(stopFile)) { if (b6 != null) await b6.ProcessCommand(app, time, users, shopId); await Task.Delay(250); }
        if (b6 != null) b6.Retain = (await File.ReadAllTextAsync(stopFile)).Trim() == "restart";
        File.Delete(stopFile); return;
    }
    using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()), Timeout = TimeSpan.FromSeconds(45) };
    string Token(int actor, string? role = null) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("pickup-test", "pickup-test",
        new[] { new Claim(ClaimTypes.NameIdentifier, users[actor].Id.ToString()), new Claim(ClaimTypes.Role, role ?? users[actor].Role) },
        expires: DateTime.UtcNow.AddHours(2), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
    async Task<(int Status, JsonElement Body, string? CacheControl, string? RetryAfter)> Send(string method, string url, int? actor, object? body = null, string? role = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (actor.HasValue) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(actor.Value, role));
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, string.IsNullOrEmpty(raw) ? default : JsonDocument.Parse(raw).RootElement.Clone(), response.Headers.CacheControl?.ToString(), response.Headers.RetryAfter?.ToString());
    }
    async Task<JsonElement> Call(string method, string url, int expected, int? actor, object? body = null, string? error = null, string? role = null)
    {
        var result = await Send(method, url, actor, body, role);
        Assert(result.Status == expected, $"{method} {url}: expected {expected}, got {result.Status}: {result.Body}");
        if (error != null) Assert(result.Body.GetProperty("errors").EnumerateArray().Any(e => e.GetString() == error), "error " + error + ": " + result.Body);
        return result.Body.ValueKind != JsonValueKind.Undefined && result.Body.TryGetProperty("data", out var data) ? data : default;
    }
    async Task<T> Db<T>(Func<AppDbContext, Task<T>> action)
    { await using var scope = app.Services.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
    async Task<CreatePickupReservationRequest> Request(int product) => new() { ProductId = products[product].Id, ClientRequestId = Guid.NewGuid(),
        ProductVersion = await Db(async db => Convert.ToBase64String((await db.Products.FindAsync(products[product].Id))!.RowVersion)),
        ScheduledAt = time.GetUtcNow().AddHours(3), ContactName = "QA buyer", ContactPhone = "0900000000", BuyerNote = "QA pickup" };
    Task<JsonElement> Create(int product, int actor) => CreateRequest(product, actor);
    async Task<JsonElement> CreateRequest(int product, int actor) => await Call("POST", "/api/pickup-reservations", 201, actor, await Request(product));
    string Path(JsonElement row, string? action = null) => $"/api/pickup-reservations/{row.GetProperty("id").GetInt32()}" + (action == null ? "" : "/" + action);
    object Version(JsonElement row) => new { version = row.GetProperty("version").GetString() };
    object Close(JsonElement row, string reason = "QA cancel") => new { version = row.GetProperty("version").GetString(), reason };

    // PASS COD exercises the real controller, authentication and SQL locks.
    var passId = await Db(async db => {
        var post = new Post { AuthorId = users[0].Id, Type = "PASS", Title = "Synthetic PASS COD", PostDescription = "Fixture", ClientRequestId = Guid.NewGuid(),
            PassInfo = new PassPostInfo { ProductName = "PASS jacket", Price = 120000, Size = "M", Defects = "Không có" } };
        db.Posts.Add(post); await db.SaveChangesAsync(); return post.Id;
    });
    await Call("GET", $"/api/Orders/pass/{passId}/quote", 401, null);
    await Call("GET", $"/api/Orders/pass/{passId}/quote", 409, 0);
    var passQuote = await Call("GET", $"/api/Orders/pass/{passId}/quote", 200, 1);
    var passVersion = passQuote.GetProperty("productVersion").GetString();
    var passRequestA = new { passPostId = passId, productVersion = passVersion, clientRequestId = Guid.NewGuid(), receiverName = "QA A", receiverPhone = "0900000000", shippingAddress = "Fixture address" };
    var passRequestB = new { passPostId = passId, productVersion = passVersion, clientRequestId = Guid.NewGuid(), receiverName = "QA B", receiverPhone = "0900000000", shippingAddress = "Fixture address" };
    var passRace = await Task.WhenAll(Send("POST", "/api/Orders/pass/cod", 1, passRequestA), Send("POST", "/api/Orders/pass/cod", 2, passRequestB));
    Assert(passRace.Count(x => x.Status == 200) == 1 && passRace.Count(x => x.Status == 409) == 1, "PASS COD concurrent HTTP: one winner");
    var passWinner = passRace.Single(x => x.Status == 200).Body.GetProperty("data"); var winningBuyer = passRace[0].Status == 200 ? 1 : 2;
    var retryPass = winningBuyer == 1 ? await Call("POST", "/api/Orders/pass/cod", 200, 1, passRequestA) : await Call("POST", "/api/Orders/pass/cod", 200, 2, passRequestB);
    Assert(retryPass.GetProperty("id").GetInt32() == passWinner.GetProperty("id").GetInt32(), "PASS COD HTTP retry is idempotent");
    Assert(passWinner.GetProperty("items")[0].GetProperty("sourceType").GetString() == "PASS_POST", "PASS source returned to client");
    await Call("GET", $"/api/Orders/{passWinner.GetProperty("id").GetInt32()}", 403, 3);
    await Call("POST", $"/api/Orders/{passWinner.GetProperty("id").GetInt32()}/collect-cod", 409, 0, Version(passWinner));
    var passShipped = await Call("POST", $"/api/Orders/{passWinner.GetProperty("id").GetInt32()}/ship", 200, 0, new { version = passWinner.GetProperty("version").GetString(), shippingCarrier = "Fixture", trackingCode = "COD1" });
    await Call("POST", $"/api/Orders/{passWinner.GetProperty("id").GetInt32()}/cancel", 409, winningBuyer, Version(passShipped));
    await Call("POST", $"/api/Orders/{passWinner.GetProperty("id").GetInt32()}/cancel", 409, 0, Version(passShipped));
    var passComplete = await Call("POST", $"/api/Orders/{passWinner.GetProperty("id").GetInt32()}/collect-cod", 200, 0, Version(passShipped));
    Assert(passComplete.GetProperty("status").GetString() == "COD_COMPLETED", "PASS COD HTTP completed");
    Assert(await Db(db => db.PassPostInfos.Where(p => p.PostId == passId).Select(p => p.SaleStatus).SingleAsync()) == "SOLD", "PASS sold in database");
    Assert(!await Db(db => db.Set<PassOrderLock>().AnyAsync(l => l.PostId == passId)), "PASS lock released after completion");
    var passCancelId = await Db(async db => {
        var post = new Post { AuthorId = users[0].Id, Type = "PASS", Title = "Synthetic PASS cancel", PostDescription = "Fixture", ClientRequestId = Guid.NewGuid(),
            PassInfo = new PassPostInfo { ProductName = "PASS shirt", Price = 90000, Size = "M", Defects = "Không có" } };
        db.Posts.Add(post); await db.SaveChangesAsync(); return post.Id;
    });
    var cancelQuote = await Call("GET", $"/api/Orders/pass/{passCancelId}/quote", 200, 1);
    var sellerCancelOrder = await Call("POST", "/api/Orders/pass/cod", 200, 1, new { passPostId = passCancelId, productVersion = cancelQuote.GetProperty("productVersion").GetString(), clientRequestId = Guid.NewGuid(), receiverName = "QA", receiverPhone = "0900000000", shippingAddress = "Fixture" });
    await Call("POST", $"/api/Orders/{sellerCancelOrder.GetProperty("id").GetInt32()}/cancel", 403, 3, Version(sellerCancelOrder));
    var sellerCancelled = await Call("POST", $"/api/Orders/{sellerCancelOrder.GetProperty("id").GetInt32()}/cancel", 200, 0, Version(sellerCancelOrder));
    Assert(sellerCancelled.GetProperty("status").GetString() == "CANCELLED" && !await Db(db => db.Set<PassOrderLock>().AnyAsync(l => l.PostId == passCancelId)), "PASS seller cancels COD before shipping and releases lock");

    await Call("GET", "/api/pickup-reservations/mine", 401, null);
    await Call("GET", "/api/pickup-reservations/mine", 403, 22, error: "ROLE_NOT_ALLOWED");
    await Call("GET", "/api/pickup-reservations/mine", 403, 23, error: "ROLE_NOT_ALLOWED", role: "Shopper");
    await Call("GET", "/api/pickup-reservations/shop", 403, 0, error: "ROLE_NOT_ALLOWED");
    await Call("GET", "/api/pickup-reservations/mine?pageSize=25", 400, 0, error: "INVALID_REQUEST");
    await Call("POST", "/api/pickup-reservations", 400, 0, new { productId = products[0].Id, buyerId = users[1].Id }, "INVALID_REQUEST");
    await Call("POST", "/api/pickup-reservations", 400, 0, new { scheduledAt = "2026-10-28T10:00:00" }, "INVALID_REQUEST");
    var invalid = await Request(0); invalid.ScheduledAt = time.GetUtcNow().AddMinutes(29);
    await Call("POST", "/api/pickup-reservations", 400, 0, invalid, "INVALID_SCHEDULE");
    invalid.ScheduledAt = time.GetUtcNow().AddHours(25);
    await Call("POST", "/api/pickup-reservations", 400, 0, invalid, "INVALID_SCHEDULE");
    await Call("POST", "/api/pickup-reservations", 403, 20, await Request(0), "ROLE_NOT_ALLOWED");
    var request0 = await Request(0);
    var first = await Call("POST", "/api/pickup-reservations", 201, 0, request0);
    var retry = await Call("POST", "/api/pickup-reservations", 200, 0, request0);
    Assert(first.GetProperty("id").GetInt32() == retry.GetProperty("id").GetInt32(), "create retry same record");
    request0.BuyerNote = "different";
    await Call("POST", "/api/pickup-reservations", 409, 0, request0, "IDEMPOTENCY_CONFLICT");
    await Call("POST", "/api/pickup-reservations", 409, 0, await Request(0), "ACTIVE_RESERVATION_EXISTS");
    Assert(await Db(db => db.Products.Where(p => p.Id == products[0].Id).Select(p => p.Status).SingleAsync()) == "AVAILABLE", "pending does not lock product");
    var otherPending = await Create(0, 1);
    await Call("GET", Path(first), 404, 2, error: "RESERVATION_NOT_FOUND");
    await Call("GET", Path(first), 404, 21, error: "RESERVATION_NOT_FOUND");
    await Call("POST", Path(first, "accept"), 403, 0, Version(first), "ROLE_NOT_ALLOWED");
    await Call("POST", Path(first, "accept"), 409, 20, new { version = "AAAAAAAAAAA=" }, "STALE_VERSION");
    var accepted = await Call("POST", Path(first, "accept"), 200, 20, Version(first));
    Assert(accepted.GetProperty("status").GetString() == "ACCEPTED" && accepted.GetProperty("storeAddress").GetString() == "QA pickup address", "acceptance snapshot");
    Assert(!accepted.TryGetProperty("pickupCode", out _) && !accepted.TryGetProperty("pickupCodeHash", out _) && !accepted.TryGetProperty("protectedPickupCode", out _), "no code leakage");
    await Call("POST", Path(first, "accept"), 200, 20, Version(first));
    var closedOther = await Call("GET", Path(otherPending), 200, 1);
    Assert(closedOther.GetProperty("status").GetString() == "CANCELLED" && closedOther.GetProperty("closeReason").GetString() == "PRODUCT_RESERVED_ELSEWHERE", "other pending cancelled atomically");
    await Db(async db => {
        var product = await db.Products.FindAsync(products[0].Id); var row = await db.PickupReservations.FindAsync(first.GetProperty("id").GetInt32());
        Assert(product!.Status == "LOCKED" && product.ReservedOrderId == null && product.ReservedPickupReservationId == row!.Id, "pickup owns lock");
        var clear = provider.CreateProtector("DooSii.Pickup.Code.v1").Unprotect(row!.ProtectedPickupCode!);
        Assert(clear.StartsWith(row.Id + ":") && clear.Split(':')[1].Length == 6 && row.PickupCodeHash!.SequenceEqual(HMACSHA256.HashData(pepper, Encoding.UTF8.GetBytes(clear))), "real protected code/HMAC");
        return true;
    });
    await Call("POST", "/api/Orders/create-escrow", 409, 2, new CreateEscrowOrderRequest { ProductId = products[0].Id, ProductVersion = (await Request(0)).ProductVersion,
        ClientRequestId = Guid.NewGuid(), ReceiverName = "QA", ReceiverPhone = "0900000000", ShippingAddress = "QA address", PaymentMethod = "COD" });
    var productVersion = (await Request(0)).ProductVersion;
    await Call("PATCH", $"/api/products/{products[0].Id}/publication", 409, 20, new { version = productVersion, isPublished = false });
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var locks = scope.ServiceProvider.GetRequiredService<IProductLockService>();
        await locks.UnlockProductAsync(products[0].Id, 987654);
        try { await locks.MarkProductAsSoldAsync(products[0].Id, 987654); throw new Exception("wrong owner sold pickup"); } catch (InvalidOperationException) { checks++; }
    }
    Assert(await Db(db => db.Products.Where(p => p.Id == products[0].Id).Select(p => p.Status).SingleAsync()) == "LOCKED", "online unlock cannot release pickup");
    var cancelled = await Call("POST", Path(first, "cancel"), 200, 0, Close(accepted));
    await Call("POST", Path(first, "cancel"), 200, 0, Close(accepted));
    await Call("POST", Path(first, "cancel"), 409, 0, Close(cancelled, "changed reason"));
    Assert(await Db(db => db.Products.Where(p => p.Id == products[0].Id).Select(p => p.Status).SingleAsync()) == "AVAILABLE", "cancel releases exact lock");
    await Call("DELETE", $"/api/products/{products[0].Id}", 409, 20);
    var rejected = await Create(1, 0);
    await Call("POST", Path(rejected, "reject"), 400, 20, Close(rejected, " "), "INVALID_REQUEST");
    await Call("POST", Path(rejected, "reject"), 200, 20, Close(rejected, "QA reject"));
    await Call("POST", Path(rejected, "reject"), 200, 20, Close(rejected, "QA reject"));
    var changed = await Create(2, 0);
    await Db(db => db.Products.Where(p => p.Id == products[2].Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 260000)));
    await Call("POST", Path(changed, "accept"), 409, 20, Version(changed), "PRODUCT_CHANGED");
    await Call("POST", Path(changed, "cancel"), 200, 0, Close(changed));

    var quota = await Task.WhenAll(Enumerable.Range(3, 4).Select(async index => await Send("POST", "/api/pickup-reservations", 10, await Request(index))));
    Assert(quota.Count(x => x.Status == 201) == 3 && quota.Count(x => x.Status == 409) == 1, "concurrent quota serializes across products");
    var duplicateRequest = await Request(7);
    var duplicates = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Send("POST", "/api/pickup-reservations", 11, duplicateRequest)));
    Assert(duplicates.Count(x => x.Status == 201) == 1 && duplicates.Count(x => x.Status == 200) == 3 && duplicates.Select(x => x.Body.GetProperty("data").GetProperty("id").GetInt32()).Distinct().Count() == 1, "parallel retry inserts once");
    var twoA = await Create(8, 12); var twoB = await Create(8, 13);
    var decisions = await Task.WhenAll(Send("POST", Path(twoA, "accept"), 20, Version(twoA)), Send("POST", Path(twoB, "accept"), 20, Version(twoB)));
    Assert(decisions.Count(x => x.Status == 200) == 1 && decisions.Count(x => x.Status == 409) == 1, "two accepts have one winner");
    Assert(await Db(db => db.PickupReservations.CountAsync(r => r.ProductId == products[8].Id && r.Status == "ACCEPTED")) == 1, "one accepted SQL record");
    var race = await Create(9, 14); var raceVersion = (await Request(9)).ProductVersion;
    var raceResults = await Task.WhenAll(Send("POST", Path(race, "accept"), 20, Version(race)), Send("POST", "/api/Orders/create-escrow", 15,
        new CreateEscrowOrderRequest { ProductId = products[9].Id, ProductVersion = raceVersion, ClientRequestId = Guid.NewGuid(), ReceiverName = "QA", ReceiverPhone = "0900000000", ShippingAddress = "QA address", PaymentMethod = "COD" }));
    Assert(raceResults.Count(x => x.Status == 200) == 1 && raceResults.Count(x => x.Status == 409) == 1, "pickup/checkout race has one winner");
    await Db(async db => { var product = await db.Products.FindAsync(products[9].Id); Assert((product!.ReservedOrderId != null) ^ (product.ReservedPickupReservationId != null), "one lock owner after race"); return true; });

    var location = await Create(10, 0); location = await Call("POST", Path(location, "accept"), 200, 20, Version(location));
    await Db(db => db.Stores.Where(s => s.Id == shopId).ExecuteUpdateAsync(s => s.SetProperty(s => s.Address, "QA changed address")));
    var locationRead = await Call("GET", Path(location), 200, 0);
    Assert(locationRead.GetProperty("storeLocationChanged").GetBoolean() && locationRead.GetProperty("storeAddress").GetString() == "QA pickup address", "location snapshot preserved and change flagged");
    await Call("POST", Path(location, "cancel"), 200, 0, Close(locationRead));
    var ban = await Create(11, 1);
    await Db(db => db.Users.Where(u => u.Id == users[1].Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, "Banned")));
    await Call("POST", Path(ban, "accept"), 403, 20, Version(ban), "STORE_NOT_ELIGIBLE");
    await Call("POST", Path(ban, "cancel"), 200, 20, Close(ban));
    var expires = await Create(12, 16);
    time.Advance(TimeSpan.FromHours(2));
    await Call("POST", Path(expires, "accept"), 409, 20, Version(expires), "RESERVATION_EXPIRED");
    Assert(await Db(db => db.PickupReservations.Where(r => r.Id == expires.GetProperty("id").GetInt32()).Select(r => r.Status).SingleAsync()) == "EXPIRED", "expiry committed despite conflict response");
    var acceptedExpiry = await Create(13, 16); acceptedExpiry = await Call("POST", Path(acceptedExpiry, "accept"), 200, 20, Version(acceptedExpiry));
    time.Advance(TimeSpan.FromHours(3.5));
    var expiredRead = await Call("GET", Path(acceptedExpiry), 200, 16);
    Assert(expiredRead.GetProperty("status").GetString() == "EXPIRED", "expiry at exact pickup deadline");
    Assert(await Db(db => db.Products.Where(p => p.Id == products[13].Id).Select(p => p.Status).SingleAsync()) == "AVAILABLE", "read releases expired pickup");
    var mine = await Call("GET", "/api/pickup-reservations/mine?pageSize=1", 200, 16);
    Assert(mine.GetProperty("items").GetArrayLength() == 1 && mine.GetProperty("totalItems").GetInt32() == 2, "private paging and expiry filter");
    await Call("GET", "/api/pickup-reservations/shop?status=EXPIRED", 200, 20);
    // A normal checkout quote also releases a past-due pickup without a worker.
    var oldPickup = await Create(15, 0); oldPickup = await Call("POST", Path(oldPickup, "accept"), 200, 20, Version(oldPickup));
    var expiryAnchor = DateTime.UtcNow;
    await Db(db => db.PickupReservations.Where(r => r.Id == oldPickup.GetProperty("id").GetInt32()).ExecuteUpdateAsync(s => s
        .SetProperty(r => r.CreatedAt, expiryAnchor.AddHours(-6)).SetProperty(r => r.ScheduledAt, expiryAnchor.AddHours(-3))
        .SetProperty(r => r.ResponseDeadline, expiryAnchor.AddHours(-4)).SetProperty(r => r.AcceptedAt, expiryAnchor.AddHours(-5))
        .SetProperty(r => r.ExpiresAt, expiryAnchor.AddHours(-3).AddMinutes(30))));
    var quote = await Call("GET", $"/api/Orders/quote/{products[15].Id}", 200, 2);
    Assert(await Db(db => db.PickupReservations.Where(r => r.Id == oldPickup.GetProperty("id").GetInt32()).Select(r => r.Status).SingleAsync()) == "EXPIRED", "checkout quote commits expired pickup cleanup");
    var cod = await Call("POST", "/api/Orders/create-escrow", 200, 2, new CreateEscrowOrderRequest { ProductId = products[15].Id,
        ProductVersion = quote.GetProperty("productVersion").GetString()!, ClientRequestId = Guid.NewGuid(), ReceiverName = "QA", ReceiverPhone = "0900000000", ShippingAddress = "QA address", PaymentMethod = "COD" });
    await Call("POST", $"/api/Orders/{cod.GetProperty("id").GetInt32()}/cancel", 200, 2, Version(cod));
    Assert(await Db(db => db.Products.Where(p => p.Id == products[15].Id).Select(p => p.Status).SingleAsync()) == "AVAILABLE", "normal COD cancellation still releases online lock");
    // Locked group members may be retained; removing or editing them remains forbidden.
    var groupId = await Db(async db => {
        var group = new ProductGroup { StoreId = shopId, Title = "QA group", Description = "QA group description" };
        db.ProductGroups.Add(group); await db.SaveChangesAsync();
        for (var i = 16; i <= 18; i++) {
            var product = await db.Products.FindAsync(products[i].Id); product!.ProductGroupId = group.Id; product.GroupPosition = i - 16;
        }
        await db.SaveChangesAsync(); return group.Id;
    });
    var grouped = await Create(16, 18); await Call("POST", Path(grouped, "accept"), 200, 20, Version(grouped));
    await using (var scope = app.Services.CreateAsyncScope()) {
        var groups = scope.ServiceProvider.GetRequiredService<IProductGroupService>();
        var group = await groups.ManageAsync(users[20].Id, groupId);
        var member = group.Products.Single(p => p.ProductId == products[16].Id);
        Assert(!member.CanEdit && !member.CanDetach, "group DTO protects accepted pickup member");
        try {
            await groups.UpdateAsync(users[20].Id, groupId, new UpdateProductGroupRequest {
                ClientRequestId = Guid.NewGuid(), Version = group.Version, Title = "QA group", Description = "QA group description", IsPublished = true,
                SharedImageUrls = new() { "https://example.invalid/qa.png" },
                Items = new() { new KeepGroupItemRequest { ProductId = products[17].Id }, new KeepGroupItemRequest { ProductId = products[18].Id } },
                RemovedItems = new() { new RemovedGroupProductRequest { ProductId = products[16].Id, Version = member.Version } }
            });
            throw new Exception("Detached locked pickup group member");
        } catch (ServiceException error) when (error.ErrorCode == "PRODUCT_NOT_EDITABLE") { checks++; }
    }
    // Missing protection config must fail closed, without partially accepting or cancelling competitors.
    var noSecret = await Create(14, 0);
    builder.Configuration["Pickup:CodePepper"] = null;
    await Call("POST", Path(noSecret, "accept"), 503, 20, Version(noSecret), "PICKUP_CODE_UNAVAILABLE");
    Assert(await Db(db => db.Products.Where(p => p.Id == products[14].Id).Select(p => p.Status).SingleAsync()) == "AVAILABLE", "missing secret leaves product available");
    // B4: private code, failed attempts, delivery, expiry, worker and transactional notifications.
    builder.Configuration["Pickup:CodePepper"] = Convert.ToBase64String(pepper);
    async Task<int> Maintain() { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<PickupMaintenanceService>().RunAsync(); }
    async Task<int> Deliver() { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<PickupNotificationDelivery>().RunAsync(); }
    object Complete(JsonElement row, string code, bool confirmed = true, string? version = null) => new {
        version = version ?? row.GetProperty("version").GetString(), pickupCode = code, confirmedPaymentReceived = confirmed };
    var receiving = await Create(20, 0); receiving = await Call("POST", Path(receiving, "accept"), 200, 20, Version(receiving));
    var privateCode = await Send("GET", Path(receiving, "pickup-code"), 0);
    Assert(privateCode.Status == 200 && privateCode.CacheControl?.Contains("no-store") == true, "code response never cached");
    var code = privateCode.Body.GetProperty("data").GetProperty("pickupCode").GetString()!;
    Assert(code.Length == 6 && code.All(char.IsAsciiDigit), "private six digit code");
    await Call("GET", Path(receiving, "pickup-code"), 403, 20, error: "ROLE_NOT_ALLOWED");
    await Call("GET", Path(receiving, "pickup-code"), 404, 2, error: "RESERVATION_NOT_FOUND");
    await Call("GET", Path(receiving, "pickup-code"), 403, 22, error: "ROLE_NOT_ALLOWED");
    await Call("POST", Path(receiving, "complete"), 403, 0, Complete(receiving, code), "ROLE_NOT_ALLOWED");
    await Call("POST", Path(receiving, "complete"), 404, 21, Complete(receiving, code), "RESERVATION_NOT_FOUND");
    await Call("POST", Path(receiving, "complete"), 400, 20, Complete(receiving, code, false), "INVALID_REQUEST");
    await Call("POST", Path(receiving, "complete"), 400, 20, Complete(receiving, "123"), "INVALID_REQUEST");
    await Call("POST", Path(receiving, "complete"), 409, 20, Complete(receiving, code, version: "AAAAAAAAAAA="), "STALE_VERSION");
    var wrong = code == "000000" ? "000001" : "000000";
    var latestVersion = receiving.GetProperty("version").GetString()!;
    var deadlineBefore = receiving.GetProperty("expiresAt").GetDateTimeOffset();
    for (var attempt = 1; attempt <= 5; attempt++)
    {
        var bad = await Send("POST", Path(receiving, "complete"), 20, Complete(receiving, wrong, version: latestVersion));
        Assert(bad.Status == (attempt == 5 ? 429 : 400), "failed attempt status " + attempt + ": " + bad.Body);
        latestVersion = bad.Body.GetProperty("data").GetProperty("version").GetString()!;
        if (attempt == 5) Assert(bad.RetryAfter == "900", "fifth failure locks for 15 minutes");
    }
    await Call("POST", Path(receiving, "complete"), 429, 20, Complete(receiving, code, version: latestVersion), "PICKUP_CODE_LOCKED");
    var stillCode = await Call("GET", Path(receiving, "pickup-code"), 200, 0);
    Assert(stillCode.GetProperty("pickupCode").GetString() == code && stillCode.GetProperty("expiresAt").GetDateTimeOffset() == deadlineBefore, "viewing code does not reset lock or extend hold");
    Assert(await Db(db => db.PickupReservations.Where(r => r.Id == receiving.GetProperty("id").GetInt32()).Select(r => r.FailedCodeAttempts).SingleAsync()) == 5, "locked calls do not add attempts");
    time.Advance(TimeSpan.FromMinutes(15));
    var afterLock = await Send("POST", Path(receiving, "complete"), 20, Complete(receiving, wrong, version: latestVersion));
    Assert(afterLock.Status == 400, "attempt window resets at unlock deadline");
    latestVersion = afterLock.Body.GetProperty("data").GetProperty("version").GetString()!;
    Assert(await Db(db => db.PickupReservations.Where(r => r.Id == receiving.GetProperty("id").GetInt32()).Select(r => r.FailedCodeAttempts).SingleAsync()) == 1, "new window starts at one failure");
    var ordersBefore = await Db(db => db.Orders.CountAsync()); var escrowsBefore = await Db(db => db.EscrowTransactions.CountAsync());
    var completedPickup = await Call("POST", Path(receiving, "complete"), 200, 20, Complete(receiving, code, version: latestVersion));
    Assert(completedPickup.GetProperty("status").GetString() == "COMPLETED", "successful pickup completion");
    var noticesBeforeRetry = await Db(db => db.Notifications.CountAsync());
    await Call("POST", Path(receiving, "complete"), 200, 20, Complete(receiving, code, version: latestVersion));
    await Call("POST", Path(receiving, "complete"), 409, 20, Complete(receiving, wrong, version: latestVersion), "INVALID_STATE");
    Assert(await Db(db => db.Notifications.CountAsync()) == noticesBeforeRetry, "completion retries do not duplicate notifications");
    await Call("GET", Path(receiving, "pickup-code"), 409, 0, error: "INVALID_STATE");
    await Db(async db => {
        var p = await db.Products.FindAsync(products[20].Id); var r = await db.PickupReservations.FindAsync(receiving.GetProperty("id").GetInt32());
        Assert(p!.Status == "SOLD" && p.ReservedPickupReservationId == null && p.ReservedOrderId == null && r!.ClosedById == users[20].Id && r.CloseReason == "PAYMENT_RECEIVED_AT_STORE", "completion owns sale and clears lock");
        Assert(await db.Orders.CountAsync() == ordersBefore && await db.EscrowTransactions.CountAsync() == escrowsBefore, "pickup never creates online order or escrow"); return true;
    });
    var corrupted = await Create(21, 2); corrupted = await Call("POST", Path(corrupted, "accept"), 200, 20, Version(corrupted));
    var material = await Db(db => db.PickupReservations.AsNoTracking().SingleAsync(r => r.Id == corrupted.GetProperty("id").GetInt32()));
    var firstMaterial = await Db(db => db.PickupReservations.AsNoTracking().SingleAsync(r => r.Id == receiving.GetProperty("id").GetInt32()));
    await Db(db => db.PickupReservations.Where(r => r.Id == material.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.ProtectedPickupCode, firstMaterial.ProtectedPickupCode)));
    await Call("GET", Path(corrupted, "pickup-code"), 503, 2, error: "PICKUP_CODE_UNAVAILABLE");
    await Db(db => db.PickupReservations.Where(r => r.Id == material.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.ProtectedPickupCode, material.ProtectedPickupCode)));
    builder.Configuration["Pickup:CodePepper"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    await Call("GET", Path(corrupted, "pickup-code"), 503, 2, error: "PICKUP_CODE_UNAVAILABLE");
    var corruptDetail = await Call("GET", Path(corrupted), 200, 2);
    await Call("POST", Path(corrupted, "complete"), 503, 20, Complete(corruptDetail, "123456"), "PICKUP_CODE_UNAVAILABLE");
    Assert(await Db(db => db.PickupReservations.Where(r => r.Id == material.Id).Select(r => r.FailedCodeAttempts).SingleAsync()) == 0, "key/pepper failure does not punish buyer");
    builder.Configuration["Pickup:CodePepper"] = Convert.ToBase64String(pepper);
    try { new PickupCodeProtection(new EphemeralDataProtectionProvider(), builder.Configuration).Read(material.Id, material.PickupCodeHash!, material.ProtectedPickupCode!); throw new Exception("lost key accepted"); }
    catch (ServiceException e) when (e.ErrorCode == "PICKUP_CODE_UNAVAILABLE") { checks++; }
    // Persistent provider restart must retain the same encrypted code.
    var keyParent = System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory.GetCurrentDirectory(), ".build-part7"));
    var keyDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(keyParent, "pickup-keys-" + Guid.NewGuid().ToString("N")));
    if (!keyDirectory.StartsWith(keyParent + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Invalid test key directory");
    Directory.CreateDirectory(keyDirectory);
    try {
        IDataProtectionProvider Persistent() => DataProtectionProvider.Create(new DirectoryInfo(keyDirectory), setup => {
            setup.SetApplicationName("DooSii"); if (OperatingSystem.IsWindows()) setup.ProtectKeysWithDpapi();
        });
        var issued = new PickupCodeProtection(Persistent(), builder.Configuration).Issue(123);
        var recovered = new PickupCodeProtection(Persistent(), builder.Configuration).Read(123, issued.Hash, issued.Protected);
        Assert(recovered.Length == 6, "code survives protected persistent key-ring restart");
    } finally { Directory.Delete(keyDirectory, true); }

    var relocating = await Create(22, 3); relocating = await Call("POST", Path(relocating, "accept"), 200, 20, Version(relocating));
    var relocatingCode = (await Call("GET", Path(relocating, "pickup-code"), 200, 3)).GetProperty("pickupCode").GetString()!;
    await using (var scope = app.Services.CreateAsyncScope()) {
        var relocationService = scope.ServiceProvider.GetRequiredService<IShopRelocationService>();
        var change = await relocationService.CreateAsync(users[20].Id, shopId, new CreateShopRelocationRequest { ClientRequestId = Guid.NewGuid(), ProposedAddress = "QA relocation B4", ProposedLatitude = 10.78, ProposedLongitude = 106.71, Reason = "QA location test" });
        var review = new ReviewShopRelocationRequest { ClientRequestId = Guid.NewGuid(), Decision = "APPROVE", Reason = "QA approved" };
        await relocationService.ReviewAsync(users[22].Id, change.Id, review);
        var noticeCount = await Db(db => db.Notifications.CountAsync());
        await relocationService.ReviewAsync(users[22].Id, change.Id, review);
        Assert(await Db(db => db.Notifications.CountAsync()) == noticeCount, "relocation retry does not duplicate pickup notices");
    }
    Assert(await Db(db => db.Notifications.AnyAsync(n => n.UserId == users[3].Id && n.Type == "PICKUP_RESERVATION" && n.Title == "Shop đã thay đổi vị trí")), "relocation alerts accepted buyer in same transaction");
    await Call("POST", Path(relocating, "complete"), 409, 20, Complete(relocating, relocatingCode), "STORE_LOCATION_CHANGED");
    await Call("POST", Path(relocating, "cancel"), 200, 3, Close(relocating));
    await Db(db => db.Users.Where(u => u.Id == users[2].Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, "Banned")));
    await Maintain();
    Assert(await Db(db => db.PickupReservations.Where(r => r.Id == material.Id).Select(r => r.Status).SingleAsync()) == "CANCELLED", "worker cancels banned buyer reservation");
    Assert(await Db(db => db.Products.Where(p => p.Id == products[21].Id).Select(p => p.Status).SingleAsync()) == "AVAILABLE", "worker releases ineligible pickup");
    var inactive = await Create(23, 4); inactive = await Call("POST", Path(inactive, "accept"), 200, 20, Version(inactive));
    await Db(db => db.Stores.Where(s => s.Id == shopId).ExecuteUpdateAsync(s => s.SetProperty(s => s.IsActive, false)));
    await Maintain();
    Assert(await Db(db => db.PickupReservations.Where(r => r.Id == inactive.GetProperty("id").GetInt32()).Select(r => r.CloseReason).SingleAsync()) == "ELIGIBILITY_LOST", "worker cancels inactive shop");
    var afterMaintenance = await Db(db => db.Notifications.CountAsync()); await Maintain();
    Assert(await Db(db => db.Notifications.CountAsync()) == afterMaintenance, "maintenance is idempotent");
    await Db(db => db.Stores.Where(s => s.Id == shopId).ExecuteUpdateAsync(s => s.SetProperty(s => s.IsActive, true)));
    var due = await Create(24, 4); due = await Call("POST", Path(due, "accept"), 200, 20, Version(due));
    var dueCode = (await Call("GET", Path(due, "pickup-code"), 200, 4)).GetProperty("pickupCode").GetString()!;
    time.Advance(TimeSpan.FromHours(3.5)); await Maintain();
    await Call("POST", Path(due, "complete"), 409, 20, Complete(due, dueCode), "RESERVATION_EXPIRED");
    Assert(await Db(db => db.Products.Where(p => p.Id == products[24].Id).Select(p => p.Status).SingleAsync()) == "AVAILABLE", "worker expiry releases exactly at deadline");
    var parallelComplete = await Create(25, 5); parallelComplete = await Call("POST", Path(parallelComplete, "accept"), 200, 20, Version(parallelComplete));
    var parallelCode = (await Call("GET", Path(parallelComplete, "pickup-code"), 200, 5)).GetProperty("pickupCode").GetString()!;
    var completionCalls = await Task.WhenAll(Send("POST", Path(parallelComplete, "complete"), 20, Complete(parallelComplete, parallelCode)), Send("POST", Path(parallelComplete, "complete"), 20, Complete(parallelComplete, parallelCode)));
    Assert(completionCalls.All(r => r.Status == 200), "parallel completion retry is idempotent");
    var completeVsCancel = await Create(26, 5); completeVsCancel = await Call("POST", Path(completeVsCancel, "accept"), 200, 20, Version(completeVsCancel));
    var lastCode = (await Call("GET", Path(completeVsCancel, "pickup-code"), 200, 5)).GetProperty("pickupCode").GetString()!;
    var finalRace = await Task.WhenAll(Send("POST", Path(completeVsCancel, "complete"), 20, Complete(completeVsCancel, lastCode)), Send("POST", Path(completeVsCancel, "cancel"), 5, Close(completeVsCancel)));
    Assert(finalRace.Count(r => r.Status == 200) == 1 && finalRace.Count(r => r.Status == 409) == 1, "completion/cancellation race has one winner");
    // This is a disposable fixture DB. Clear the synthetic PASS order so the
    // older outbox rollback test can reach its own guard instead of the PASS guard.
    await Db(db => db.Orders.Where(o => o.Id == passWinner.GetProperty("id").GetInt32() || o.Id == sellerCancelOrder.GetProperty("id").GetInt32()).ExecuteDeleteAsync());
    var pendingOutbox = await Db(db => db.PickupNotificationOutbox.CountAsync());
    var persistedNotifications = await Db(db => db.Notifications.CountAsync());
    await Db(async db => {
        try { await db.GetService<IMigrator>().MigrateAsync("20261027162606_AddPickupReservations"); throw new Exception("Rollback discarded pending pickup outbox"); }
        catch (SqlException error) when (error.Number == 51011) { checks++; }
        return true;
    });
    hub.Fail = true; await Deliver();
    Assert(await Db(db => db.PickupNotificationOutbox.CountAsync()) == pendingOutbox && await Db(db => db.PickupNotificationOutbox.AnyAsync(o => o.Attempts > 0)), "failed realtime is retained with backoff");
    Assert(await Db(db => db.Notifications.CountAsync()) == persistedNotifications, "push failure never loses REST notifications");
    hub.Fail = false; time.Advance(TimeSpan.FromSeconds(11));
    await Task.WhenAll(Deliver(), Deliver());
    for (var batch = 0; batch < 10 && await Db(db => db.PickupNotificationOutbox.AnyAsync()); batch++) await Deliver();
    Assert(!await Db(db => db.PickupNotificationOutbox.AnyAsync()), "outbox retries drain across concurrent publishers");
    Assert(await Db(db => db.Notifications.CountAsync()) == persistedNotifications, "delivery retains notification history");
    var payloads = hub.Delivered.Where(x => x.Notice.Type == "PICKUP_RESERVATION").ToArray();
    Assert(payloads.All(x => x.Group == $"user_{x.Notice.UserId}" && !x.Notice.Message.Contains(code) && !x.Notice.Message.Contains("0900000000")), "realtime scoped per recipient without code or contact");
    await Db(async db => {
        var migration = db.GetService<IMigrator>();
        await migration.MigrateAsync("20261027162606_AddPickupReservations"); await migration.MigrateAsync();
        Assert(await db.Notifications.CountAsync() == persistedNotifications && !await db.PickupNotificationOutbox.AnyAsync(), "empty outbox Down/Up preserves notifications");
        Assert(!db.Database.HasPendingModelChanges(), "B4 model and migration match"); return true;
    });
    Console.WriteLine($"PASS: {checks} HTTP/SQL/behavior assertions, B3 regression plus B4 code/completion/worker/outbox/relocation.");
    void Assert(bool ok, string message) { if (!ok) throw new Exception(message); Interlocked.Increment(ref checks); }
}
finally
{
    if (app != null) { await app.StopAsync(); await app.DisposeAsync(); }
    SqlConnection.ClearAllPools();
    if (!database.StartsWith("DoosiiPickupHttpTest_", StringComparison.Ordinal) || database.Length != 53) throw new Exception("Unexpected cleanup target");
    if (b6?.Retain == true) Console.WriteLine("B6 fixture stopped for process restart; resume explicitly with --ui --b6 --resume.");
    else
    {
        await using var cleanup = new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];", master);
        await cleanup.ExecuteNonQueryAsync(); Console.WriteLine("Removed disposable database " + database);
    }
}
sealed class FixtureClock(bool realtime = false) : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    private long offsetTicks;
    public long OffsetTicks => Interlocked.Read(ref offsetTicks);
    public override DateTimeOffset GetUtcNow() => realtime ? DateTimeOffset.UtcNow.AddTicks(OffsetTicks) : now;
    public void Advance(TimeSpan duration) { if (realtime) Interlocked.Add(ref offsetTicks, duration.Ticks); else now += duration; }
}
sealed class FixtureControllers : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        var allowed = new[] { typeof(PickupReservationsController), typeof(OrdersController), typeof(ProductsController), typeof(StoresController), typeof(NotificationsController) };
        foreach (var controller in feature.Controllers.ToList()) if (!allowed.Contains(controller.AsType())) feature.Controllers.Remove(controller);
    }
}
sealed class PickupFixtureChatHub : Hub { }
sealed class RecordingHub : IHubContext<NotificationHub>
{
    public bool Fail;
    public System.Collections.Concurrent.ConcurrentBag<(string Group, NotificationDto Notice)> Delivered { get; } = new();
    public IHubClients Clients => new RecordingClients(this);
    public IGroupManager Groups => new RecordingGroups();
    private sealed class Proxy(RecordingHub owner, string target) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (owner.Fail) throw new IOException("Synthetic push failure");
            if (args.FirstOrDefault() is NotificationDto notice) owner.Delivered.Add((target, notice));
            return Task.CompletedTask;
        }
    }
    private sealed class RecordingClients(RecordingHub owner) : IHubClients
    {
        public IClientProxy All => new Proxy(owner, "all");
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => All;
        public IClientProxy Client(string connectionId) => new Proxy(owner, connectionId);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => All;
        public IClientProxy Group(string groupName) => new Proxy(owner, groupName);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => All;
        public IClientProxy User(string userId) => new Proxy(owner, userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => All;
    }
    private sealed class RecordingGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
