using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Doosii.API.Controllers;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.BLL.Services;
using Doosii.DAL.Data;
using Doosii.DAL.Models;
using Doosii.DAL.Models.Store;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

// Uses actual SQL Server, actual controllers/model binding/JWT and a deterministic
// storage fake. Never starts production Program, seeders, workers or a real upload.
var databaseName = "DoosiiCommunityTest_" + Guid.NewGuid().ToString("N");
var masterConnection = "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;Encrypt=false";
var connection = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = databaseName }.ConnectionString;
var master = new SqlConnection(masterConnection);
await master.OpenAsync();
await using (var create = new SqlCommand($"CREATE DATABASE [{databaseName}]", master)) await create.ExecuteNonQueryAsync();
WebApplication? app = null;
int passed = 0;
var fakeStorage = new FakeStorage();
var jwtKey = new SymmetricSecurityKey(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
var uiMode = args.Contains("--ui");
try
{
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseUrls(uiMode ? "http://127.0.0.1:5257" : "http://127.0.0.1:0");
    builder.Logging.ClearProviders(); builder.Logging.AddConsole(); builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connection));
    builder.Services.AddSingleton<ICommunityMediaStorage>(fakeStorage);
    builder.Services.AddScoped<ICommunityPostService, CommunityPostService>();
    builder.Services.AddScoped<ICommunityInteractionService, CommunityInteractionService>();
    builder.Services.AddScoped<IPostReportService, PostReportService>();
    builder.Services.AddScoped<IProductService, ProductService>();
    builder.Services.AddScoped<IStoreService, StoreService>();
    builder.Services.AddScoped<CommunityMediaCleanupService>();
    builder.Services.AddControllers().AddApplicationPart(typeof(CommunityController).Assembly)
        .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new CommunityFixtureControllers()));
    builder.Services.AddSignalR();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new() {
        ValidateIssuer = true, ValidIssuer = "community-tests", ValidateAudience = true, ValidAudience = "community-tests",
        ValidateIssuerSigningKey = true, IssuerSigningKey = jwtKey, ValidateLifetime = true, ClockSkew = TimeSpan.Zero });
    builder.Services.AddAuthorization();
    app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
    app.MapGet("/api/test-media/{key}", (string key) => fakeStorage.Assets.TryGetValue(key, out var asset) ? Results.File(asset.Bytes, asset.Mime) : Results.NotFound());
    int casual, casual2, shopper, shopper2, admin, store1, store2, product1, product2;
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.MigrateAsync();
        var users = new[] { "CasualUser", "CasualUser", "Shopper", "Shopper", "Admin" }.Select((role, i) => new User { Email = $"test{i}@example.invalid", FullName = $"User {i}", PasswordHash = "not-a-login", Role = role }).ToArray();
        db.Users.AddRange(users); await db.SaveChangesAsync();
        casual = users[0].Id; casual2 = users[1].Id; shopper = users[2].Id; shopper2 = users[3].Id; admin = users[4].Id;
        db.MerchantProfiles.AddRange(new MerchantProfile { UserId = shopper, KycStatus = "APPROVED" }, new MerchantProfile { UserId = shopper2, KycStatus = "APPROVED" });
        var shops = new[] { new Store { OwnerId = shopper, Name = "Shop A", Latitude = 10.77, Longitude = 106.70 }, new Store { OwnerId = shopper2, Name = "Shop B", Latitude = 10.771, Longitude = 106.701 } };
        db.Stores.AddRange(shops); var category = new Category { Name = "Test", Slug = "test" }; db.Categories.Add(category); await db.SaveChangesAsync();
        store1 = shops[0].Id; store2 = shops[1].Id;
        var products = new[] { new Product { StoreId = store1, CategoryId = category.Id, Title = "Product A", Price = 100 }, new Product { StoreId = store2, CategoryId = category.Id, Title = "Product B", Price = 200 } };
        db.Products.AddRange(products); await db.SaveChangesAsync(); product1 = products[0].Id; product2 = products[1].Id;
    }
    string FixtureToken(User u) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("community-tests", "community-tests", new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Role, u.Role) }, expires: DateTime.UtcNow.AddHours(2), signingCredentials: new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha256)));
    if (uiMode)
    {
        app.MapPost("/api/Auth/login", async (LoginRequest request, AppDbContext db) =>
        {
            var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == request.Email);
            if (u == null || request.Password != "community-ui-test") return Results.Unauthorized();
            return Results.Ok(ApiResponse.Ok(new AuthResponse { Token = FixtureToken(u), RefreshToken = "fixture-refresh", User = new UserDto { Id = u.Id, Email = u.Email, FullName = u.FullName, Role = u.Role } }));
        });
        app.MapGet("/api/Auth/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var id = int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var u = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
            return Results.Ok(ApiResponse.Ok(new UserDto { Id = u.Id, Email = u.Email, FullName = u.FullName, Role = u.Role }));
        }).RequireAuthorization();
        app.MapGet("/api/Notifications/unread-count", () => Results.Ok(ApiResponse.Ok(new { unreadCount = 0 }))).RequireAuthorization();
        app.MapGet("/api/Notifications", () => Results.Ok(ApiResponse.Ok(Array.Empty<object>()))).RequireAuthorization();
        app.MapHub<CommunityFixtureHub>("/hubs/chat"); app.MapHub<CommunityFixtureHub>("/hubs/notifications");
        await app.StartAsync();
        var stopFile = Path.Combine(Directory.GetCurrentDirectory(), ".build-part7", "community-ui.stop");
        if (File.Exists(stopFile)) File.Delete(stopFile);
        Console.WriteLine($"UI fixture ready at http://127.0.0.1:5257; test0/test1 CasualUser, test2/test3 Shopper, test4 Admin @example.invalid; password community-ui-test. Stores {store1},{store2}; products {product1},{product2}. Create {stopFile} to stop and clean the disposable DB.");
        while (!File.Exists(stopFile)) await Task.Delay(1000);
        File.Delete(stopFile);
        return;
    }
    await app.StartAsync();
    using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    string Token(int id, string role = "CasualUser") => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("community-tests", "community-tests", new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role) }, expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha256)));
    async Task<JsonElement> Request(HttpMethod method, string path, int expected, int? actor = null, object? body = null, HttpContent? content = null, string role = "CasualUser", string? error = null)
    {
        using var req = new HttpRequestMessage(method, path);
        if (actor.HasValue) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(actor.Value, role));
        req.Content = content ?? (body == null ? null : JsonContent.Create(body));
        using var response = await client.SendAsync(req);
        var raw = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode != expected) throw new Exception($"{method} {path}: expected {expected}, got {(int)response.StatusCode}: {raw}");
        passed++;
        if (string.IsNullOrEmpty(raw)) return default;
        var json = JsonDocument.Parse(raw).RootElement.Clone();
        if (error != null && !json.GetProperty("errors").EnumerateArray().Any(e => e.GetString() == error)) throw new Exception($"Expected {error}: {raw}");
        return json.TryGetProperty("data", out var data) ? data.Clone() : json;
    }
    CommunityPostWriteRequest Other(params CommunityMediaWrite[] media) => new() { Type = "OTHER", ClientRequestId = Guid.NewGuid(), Title = "Other title", PostDescription = "Other description", Media = media.ToList() };
    CommunityPostWriteRequest Pass() => new() { Type = "PASS", ClientRequestId = Guid.NewGuid(), Title = "Pass my jacket", PostDescription = "Looking for a new owner", PassInfo = new() { ProductName = "Denim", Price = 100, Size = "M", ConditionPercent = 90, Defects = "Không có", StyleTagIds = new() { "90s-vintage" }, Measurements = new() { new ProductMeasurement { Name = "Vai", Unit = "cm", Value = 44 } } }, Media = new() { new CommunityMediaWrite { UploadKey = "front", MediaType = "IMAGE", DisplayOrder = 0 } } };
    MultipartFormDataContent Form(CommunityPostWriteRequest r)
    {
        var form = new MultipartFormDataContent(); form.Add(new StringContent(JsonSerializer.Serialize(r, new JsonSerializerOptions(JsonSerializerDefaults.Web))), "payload");
        foreach (var m in r.Media.Where(m => m.Id == null))
        {
            byte[] bytes = m.MediaType == "VIDEO" ? new byte[] { 0,0,0,24,102,116,121,112,105,115,111,109,0,0,0,0 } : Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6pSAAAAAASUVORK5CYII=");
            var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue(m.MediaType == "VIDEO" ? "video/mp4" : "image/png");
            form.Add(file, m.UploadKey!, m.MediaType == "VIDEO" ? "video.mp4" : "image.png");
        }
        return form;
    }
    Task<JsonElement> Create(CommunityPostWriteRequest r, int actor, int expected = 201, string? error = null) => Request(HttpMethod.Post, "/api/community/posts", expected, actor, content: Form(r), error: error);
    var text = await Create(Other(), casual); var textId = text.GetProperty("id").GetInt32();
    var concurrent = Other();
    var concurrentResults = await Task.WhenAll(Create(concurrent, casual), Create(concurrent, casual));
    Assert(concurrentResults[0].GetProperty("id").GetInt32() == concurrentResults[1].GetProperty("id").GetInt32(), "concurrent duplicate creates share one post");
    var passRequest = Pass(); var pass = await Create(passRequest, casual); int passId = pass.GetProperty("id").GetInt32();
    Assert(pass.GetProperty("passInfo").GetProperty("productName").GetString() == "Denim", "separate product title");
    int beforeDuplicate = fakeStorage.Uploads;
    var retry = await Create(passRequest, casual); Assert(retry.GetProperty("id").GetInt32() == passId && fakeStorage.Uploads == beforeDuplicate, "create idempotency avoids reupload");
    passRequest.Title = "Changed retry"; await Create(passRequest, casual, 409, "CLIENT_REQUEST_CONFLICT");
    await Create(Pass(), shopper, 403, "PASS_ROLE_REQUIRED");
    var invalidPass = Pass(); invalidPass.Media[0].StoreId = store1; await Create(invalidPass, casual, 400, "PASS_TAG_NOT_ALLOWED");
    var videoOnlyPass = Pass(); videoOnlyPass.Media[0].MediaType = "VIDEO"; await Create(videoOnlyPass, casual, 400, "INVALID_MEDIA");
    var badMeasurement = Pass(); badMeasurement.PassInfo!.Measurements![0].Value = -1; await Create(badMeasurement, casual, 400, "INVALID_MEASUREMENTS");
    var ownShopRequest = Other(new CommunityMediaWrite() { UploadKey = "shop", MediaType = "IMAGE", DisplayOrder = 0, ProductId = product1 });
    var ownShop = await Create(ownShopRequest, shopper); int ownId = ownShop.GetProperty("id").GetInt32();
    Assert(ownShop.GetProperty("media")[0].GetProperty("store").GetProperty("id").GetInt32() == store1, "store inferred from product");
    await Request(HttpMethod.Delete, $"/api/products/{product1}", 409, shopper, role: "Shopper");
    await Create(Other(new CommunityMediaWrite() { UploadKey = "shop", MediaType = "IMAGE", StoreId = store2 }), shopper, 403, "ATTACHMENT_NOT_OWNED");
    await Create(Other(new CommunityMediaWrite() { UploadKey = "shop", MediaType = "IMAGE", ProductId = product2 }), shopper, 403, "ATTACHMENT_NOT_OWNED");
    await Create(Other(new CommunityMediaWrite() { UploadKey = "shop", MediaType = "IMAGE", StoreId = store2, ProductId = product1 }), casual, 400, "PRODUCT_STORE_MISMATCH");
    var multiRequest = Other(new CommunityMediaWrite() { UploadKey = "a", MediaType = "IMAGE", StoreId = store1, DisplayOrder = 0 }, new CommunityMediaWrite() { UploadKey = "b", MediaType = "VIDEO", ProductId = product2, DisplayOrder = 1 });
    var multi = await Create(multiRequest, casual); var multiId = multi.GetProperty("id").GetInt32();
    var near = await Request(HttpMethod.Get, "/api/community/posts?tab=near-you&latitude=10.77&longitude=106.70&radiusKm=5", 200);
    Assert(near.GetProperty("items").EnumerateArray().Count(p => p.GetProperty("id").GetInt32() == multiId) == 1, "nearby deduplicates multiple shops");
    Assert(!near.GetProperty("items").EnumerateArray().Any(p => p.GetProperty("type").GetString() == "PASS"), "PASS excluded from nearby");
    await Request(HttpMethod.Get, "/api/community/posts?tab=near-you&latitude=100&longitude=0", 400, error: "INVALID_COORDINATES");
    var byStyle = await Request(HttpMethod.Get, "/api/community/posts?tab=by-style&style=90s-vintage", 200);
    Assert(byStyle.GetProperty("items").EnumerateArray().Any(p => p.GetProperty("id").GetInt32() == passId), "style feed");
    var storeFeed = await Request(HttpMethod.Get, $"/api/community/posts?storeId={store1}", 200);
    Assert(storeFeed.GetProperty("totalItems").GetInt32() == 2, "store feed includes inferred product stores");
    // Swap the two media positions using actual multipart model binding.
    var edit = Other(); edit.Version = multi.GetProperty("version").GetString();
    edit.Media = multi.GetProperty("media").EnumerateArray().Select((m, i) => new CommunityMediaWrite { Id = m.GetProperty("id").GetInt32(), MediaType = m.GetProperty("mediaType").GetString()!, DisplayOrder = 1 - i }).ToList();
    var edited = await Request(HttpMethod.Put, $"/api/community/posts/{multiId}", 200, casual, content: Form(edit));
    Assert(edited.GetProperty("version").GetString() != multi.GetProperty("version").GetString(), "child edits bump version");
    await Request(HttpMethod.Put, $"/api/community/posts/{multiId}", 409, casual, content: Form(edit), error: "COMMUNITY_CONFLICT");
    // Recheck ownership on edit, even for media that already belongs to this post.
    var shopperEdit = Other(new CommunityMediaWrite { Id = ownShop.GetProperty("media")[0].GetProperty("id").GetInt32(), MediaType = "IMAGE", StoreId = store2 });
    shopperEdit.Version = ownShop.GetProperty("version").GetString();
    await Request(HttpMethod.Put, $"/api/community/posts/{ownId}", 403, shopper, content: Form(shopperEdit), error: "ATTACHMENT_NOT_OWNED");
    edit.Version = edited.GetProperty("version").GetString(); edit.Media[0].Id = ownShop.GetProperty("media")[0].GetProperty("id").GetInt32();
    await Request(HttpMethod.Put, $"/api/community/posts/{multiId}", 400, casual, content: Form(edit), error: "INVALID_MEDIA");
    await Request(HttpMethod.Put, $"/api/community/posts/{multiId}", 403, casual2, content: Form(edit), error: "POST_NOT_OWNED");
    // Remove stale attachments by editing to plain text, and verify cleanup is durable.
    var plain = Other(); plain.Version = edited.GetProperty("version").GetString();
    await Request(HttpMethod.Put, $"/api/community/posts/{multiId}", 200, casual, content: Form(plain));
    await using (var scope = app.Services.CreateAsyncScope()) { await scope.ServiceProvider.GetRequiredService<CommunityMediaCleanupService>().ProcessAsync(); }
    Assert(fakeStorage.Deleted.Count >= 2, "detached media cleanup includes video");
    await Request(HttpMethod.Put, $"/api/community/posts/{passId}/reaction", 401);
    await Request(HttpMethod.Put, $"/api/community/posts/{passId}/reaction", 200, casual2);
    var liked = await Request(HttpMethod.Put, $"/api/community/posts/{passId}/reaction", 200, casual2);
    Assert(liked.GetProperty("reactionCount").GetInt32() == 1, "likes idempotent");
    var commentRequest = new CommunityCommentRequest { Content = "Interested", ClientRequestId = Guid.NewGuid() };
    var comment = await Request(HttpMethod.Post, $"/api/community/posts/{passId}/comments", 201, casual2, commentRequest);
    var duplicateComment = await Request(HttpMethod.Post, $"/api/community/posts/{passId}/comments", 201, casual2, commentRequest);
    Assert(comment.GetProperty("id").GetInt32() == duplicateComment.GetProperty("id").GetInt32(), "comment retry idempotent");
    int commentId = comment.GetProperty("id").GetInt32();
    var reply = await Request(HttpMethod.Post, $"/api/community/posts/{passId}/comments", 201, casual, new CommunityCommentRequest { Content = "Thanks", ClientRequestId = Guid.NewGuid(), ParentCommentId = commentId });
    await Request(HttpMethod.Post, $"/api/community/posts/{textId}/comments", 400, casual, new CommunityCommentRequest { Content = "Wrong post", ClientRequestId = Guid.NewGuid(), ParentCommentId = commentId }, error: "INVALID_COMMENT_PARENT");
    await Request(HttpMethod.Post, $"/api/community/posts/{passId}/comments", 400, casual, new CommunityCommentRequest { Content = "Too deep", ClientRequestId = Guid.NewGuid(), ParentCommentId = reply.GetProperty("id").GetInt32() }, error: "INVALID_COMMENT_PARENT");
    await Request(HttpMethod.Delete, $"/api/community/posts/{passId}/comments/{commentId}", 200, casual2);
    var tombstone = await Request(HttpMethod.Get, $"/api/community/posts/{passId}/comments", 200);
    Assert(tombstone.GetProperty("items")[0].GetProperty("isDeleted").GetBoolean(), "comment tombstone preserves replies");
    var sold = await Request(HttpMethod.Patch, $"/api/community/posts/{passId}/sale-status", 200, casual, new { saleStatus = "SOLD", version = pass.GetProperty("version").GetString() });
    Assert(sold.GetProperty("saleStatus").GetString() == "SOLD", "PASS marked sold");
    var report = await Request(HttpMethod.Post, $"/api/community/posts/{passId}/reports", 201, casual2, new { reason = "SCAM" }); int reportId = report.GetProperty("id").GetInt32();
    await Request(HttpMethod.Post, $"/api/community/posts/{passId}/reports", 409, casual2, new { reason = "OTHER" }, error: "REPORT_ALREADY_PENDING");
    await Request(HttpMethod.Get, "/api/community/moderation/reports", 403, casual);
    var queue = await Request(HttpMethod.Get, "/api/community/moderation/reports", 200, admin, role: "Admin"); Assert(queue.GetProperty("totalItems").GetInt32() == 1, "admin queue");
    await Request(HttpMethod.Put, $"/api/community/moderation/reports/{reportId}/resolve", 200, admin, new { action = "HIDE_POST", adminNote = "Test moderation" }, role: "Admin");
    await Request(HttpMethod.Get, $"/api/community/posts/{passId}", 404);
    await Request(HttpMethod.Put, $"/api/community/posts/{passId}/reaction", 404, casual2);
    await Request(HttpMethod.Get, $"/api/community/posts/{passId}/comments", 404);
    var withoutHidden = await Request(HttpMethod.Get, "/api/community/posts", 200);
    Assert(!withoutHidden.GetProperty("items").EnumerateArray().Any(p => p.GetProperty("id").GetInt32() == passId), "moderated post absent from feed");
    var hidden = await Request(HttpMethod.Get, $"/api/community/posts/{passId}", 200, casual);
    await Request(HttpMethod.Patch, $"/api/community/posts/{passId}/visibility", 403, casual, new { visibilityStatus = "ACTIVE", version = hidden.GetProperty("version").GetString() }, error: "POST_MODERATED");
    await Request(HttpMethod.Get, $"/api/community/moderation/reports/{reportId}/post", 200, admin, role: "Admin");
    await Request(HttpMethod.Put, $"/api/community/moderation/reports/{reportId}/resolve", 409, admin, new { action = "DISMISS" }, role: "Admin", error: "REPORT_ALREADY_RESOLVED");
    // Attachment privacy after the shop/product is no longer public.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Products.Where(p => p.Id == product1).ExecuteUpdateAsync(p => p.SetProperty(p => p.IsPublished, false));
    }
    var unavailable = await Request(HttpMethod.Get, $"/api/community/posts/{ownId}", 200);
    Assert(unavailable.GetProperty("media")[0].GetProperty("attachmentUnavailable").GetBoolean() && unavailable.GetProperty("media")[0].GetProperty("store").ValueKind == JsonValueKind.Null, "hidden attachment redaction");
    await Create(Other(new CommunityMediaWrite() { UploadKey = "product", MediaType = "IMAGE", ProductId = product1 }), casual, 404, "ATTACHMENT_NOT_AVAILABLE");
    // Actor role is re-read from DB, rather than trusting the JWT role.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Users.Where(u => u.Id == casual2).ExecuteUpdateAsync(u => u.SetProperty(u => u.Role, "Banned"));
    }
    await Create(Other(), casual2, 403, "USER_BANNED");
    // A non-admin actor cannot use a forged/stale Admin role claim to read the queue.
    await Request(HttpMethod.Get, "/api/community/moderation/reports", 403, casual, role: "Admin", error: "FORBIDDEN");
    // Upload failure leaves no partial post and schedules deletion of successful/inflight uploads.
    fakeStorage.FailNext = true;
    await Create(Other(new CommunityMediaWrite() { UploadKey = "fail", MediaType = "IMAGE" }), casual, 502, "UPLOAD_FAILED");
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert(await db.CommunityMediaCleanup.CountAsync() == 1, "failed upload retained in cleanup outbox");
        await scope.ServiceProvider.GetRequiredService<CommunityMediaCleanupService>().ProcessAsync();
        Assert(await db.CommunityMediaCleanup.CountAsync() == 0, "failed upload cleanup processed");
    }
    // Enforce the daily PASS limit while preserving existing hidden posts in the count.
    for (int i = 0; i < 9; i++) await Create(Pass(), casual);
    await Create(Pass(), casual, 429, "PASS_DAILY_LIMIT");
    // Cleanup failures remain durable and retryable, rather than losing deletion work.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CommunityMediaCleanup.Add(new() { PublicId = "test/retry", MediaType = "VIDEO", DeleteAfter = DateTime.UtcNow }); await db.SaveChangesAsync();
        fakeStorage.FailDelete = true; await scope.ServiceProvider.GetRequiredService<CommunityMediaCleanupService>().ProcessAsync();
        Assert(await db.CommunityMediaCleanup.AnyAsync(q => q.PublicId == "test/retry" && q.Attempts == 1), "cleanup failure is rescheduled");
        await db.CommunityMediaCleanup.Where(q => q.PublicId == "test/retry").ExecuteUpdateAsync(q => q.SetProperty(q => q.DeleteAfter, DateTime.UtcNow));
    }
    await using (var scope = app.Services.CreateAsyncScope())
    {
        await scope.ServiceProvider.GetRequiredService<CommunityMediaCleanupService>().ProcessAsync();
        Assert(!await scope.ServiceProvider.GetRequiredService<AppDbContext>().CommunityMediaCleanup.AnyAsync(), "cleanup retry succeeds");
    }
    Console.WriteLine($"PASS: {passed} HTTP requests and behavioral assertions; actual SQL/JWT/multipart, fake external storage.");
    void Assert(bool condition, string message) { if (!condition) throw new Exception("Assertion failed: " + message); passed++; }
}
finally
{
    if (app != null) { await app.StopAsync(); await app.DisposeAsync(); }
    SqlConnection.ClearAllPools();
    if (!databaseName.StartsWith("DoosiiCommunityTest_", StringComparison.Ordinal) || databaseName.Length != 52) throw new Exception("Unexpected test database name; refusing cleanup.");
    await using var cleanup = new SqlCommand($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];", master);
    await cleanup.ExecuteNonQueryAsync(); await master.DisposeAsync();
}

sealed class FakeStorage : ICommunityMediaStorage
{
    public System.Collections.Concurrent.ConcurrentDictionary<string, (byte[] Bytes, string Mime)> Assets { get; } = new();
    public int Uploads;
    public bool FailNext;
    public bool FailDelete;
    public List<string> Deleted { get; } = new();
    public async Task<CommunityUploadedMedia> UploadAsync(Microsoft.AspNetCore.Http.IFormFile file, string mediaType, string publicId)
    {
        Uploads++;
        if (FailNext) { FailNext = false; throw new Doosii.BLL.Exceptions.ServiceException(502, "Simulated upload failure", "UPLOAD_FAILED"); }
        await using var stream = file.OpenReadStream(); using var buffer = new MemoryStream(); await stream.CopyToAsync(buffer);
        var key = publicId.Replace('/', '_') + (mediaType == "VIDEO" ? ".mp4" : ".png"); Assets[key] = (buffer.ToArray(), file.ContentType);
        return new CommunityUploadedMedia(publicId, mediaType, "/api/test-media/" + key, null);
    }
    public Task DeleteAsync(string publicId, string mediaType)
    {
        if (FailDelete) { FailDelete = false; throw new InvalidOperationException("Simulated delete failure"); }
        Deleted.Add(mediaType + ":" + publicId); return Task.CompletedTask;
    }
}

sealed class CommunityFixtureControllers : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        var allowed = new[] { typeof(CommunityController), typeof(CommunityInteractionsController), typeof(StoresController), typeof(ProductsController) };
        foreach (var controller in feature.Controllers.ToList()) if (!allowed.Contains(controller.AsType())) feature.Controllers.Remove(controller);
    }
}
sealed class CommunityFixtureHub : Microsoft.AspNetCore.SignalR.Hub { }
