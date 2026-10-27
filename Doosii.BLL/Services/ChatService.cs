using System.Data;
using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;
using Doosii.DAL.Models.Store;

namespace Doosii.BLL.Services;
public class ChatService : IChatService
{
    private readonly AppDbContext _context;
    public ChatService(AppDbContext context) { _context = context; }
    private static ServiceException Fail(int status, string message, string code) => new(status, message, code);
    private async Task UserAsync(int id) { if (!await _context.Users.AnyAsync(u => u.Id == id && u.Role != "Banned")) throw Fail(403, "Tài khoản không được phép dùng chat.", "CHAT_ACCESS_DENIED"); }
    private IQueryable<Conversation> Conversations() => _context.Conversations.Include(c => c.Buyer).Include(c => c.Seller).Include(c => c.Product).ThenInclude(p => p!.Images)
        .Include(c => c.Product).ThenInclude(p => p!.Store).ThenInclude(s => s.Owner).ThenInclude(u => u.MerchantProfile);
    private IQueryable<ChatMessage> Messages() => _context.ChatMessages.Include(m => m.Sender).Include(m => m.Product).ThenInclude(p => p!.Images)
        .Include(m => m.Product).ThenInclude(p => p!.Store).ThenInclude(s => s.Owner).ThenInclude(u => u.MerchantProfile);
    private static bool Public(Product p) => p.IsPublished && p.Store.IsActive && p.Store.Owner.Role == "Shopper" && p.Store.Owner.MerchantProfile?.KycStatus == "APPROVED";
    public async Task<bool> IsMemberAsync(int conversationId, int userId) { await UserAsync(userId); return await _context.Conversations.AnyAsync(c => c.Id == conversationId && (c.BuyerId == userId || c.SellerId == userId)); }
    public async Task<ConversationDto> GetConversationAsync(int conversationId, int userId)
    {
        if (!await IsMemberAsync(conversationId, userId)) throw Fail(403, "Bạn không có quyền xem hội thoại này.", "CHAT_ACCESS_DENIED");
        return await ConversationDtoAsync(await Conversations().AsNoTracking().SingleAsync(c => c.Id == conversationId), userId);
    }
    public async Task<ConversationDto> GetOrCreateConversationAsync(int buyerId, int sellerId, int? productId)
    {
        await UserAsync(buyerId);
        if (buyerId == sellerId) throw Fail(400, "Bạn không thể nhắn shop của mình.", "CANNOT_CHAT_WITH_SELF");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        // Serialize retries, including general chat whose nullable product is outside the unique index filter.
        await _context.Users.FromSqlInterpolated($"SELECT * FROM [auth].[Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {buyerId}").LoadAsync();
        if (!await _context.Stores.AnyAsync(s => s.OwnerId == sellerId && s.IsActive && s.Owner.Role == "Shopper" && s.Owner.MerchantProfile != null && s.Owner.MerchantProfile.KycStatus == "APPROVED")) throw Fail(404, "Shop không còn hoạt động.", "SHOP_NOT_ACTIVE");
        if (productId.HasValue && !await _context.Products.AnyAsync(p => p.Id == productId && p.Store.OwnerId == sellerId && p.IsPublished && p.Store.IsActive)) throw Fail(404, "Sản phẩm không thuộc shop hoặc không công khai.", "PRODUCT_NOT_FOUND");
        var conversation = await Conversations().SingleOrDefaultAsync(c => c.BuyerId == buyerId && c.SellerId == sellerId && c.ProductId == productId);
        if (conversation == null) { conversation = new Conversation { BuyerId = buyerId, SellerId = sellerId, ProductId = productId }; _context.Conversations.Add(conversation); await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); conversation = await Conversations().SingleAsync(c => c.Id == conversation.Id); }
        await tx.CommitAsync(); return await ConversationDtoAsync(conversation, buyerId);
    }
    public async Task<PagedResult<ConversationDto>> GetConversationsAsync(int userId, GetConversationsQuery query)
    {
        await UserAsync(userId); ValidatePage(query.Page, query.PageSize, 50);
        var rows = Conversations().AsNoTracking().Where(c => c.BuyerId == userId || c.SellerId == userId);
        var count = await rows.CountAsync();
        var conversations = await rows.OrderByDescending(c => c.LastMessageAt).ThenByDescending(c => c.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();
        var items = new List<ConversationDto>(); foreach (var c in conversations) items.Add(await ConversationDtoAsync(c, userId));
        return new() { Items = items, TotalCount = count, Page = query.Page, PageSize = query.PageSize };
    }
    private static void ValidatePage(int page, int pageSize, int max) { if (page < 1 || page > 1000000 || pageSize < 1 || pageSize > max) throw Fail(400, "Phân trang không hợp lệ.", "INVALID_QUERY"); }
    public async Task<ChatMessagesPageDto> GetMessagesAsync(int conversationId, int userId, GetMessagesQuery query)
    {
        if (!await IsMemberAsync(conversationId, userId)) throw Fail(403, "Bạn không có quyền xem hội thoại này.", "CHAT_ACCESS_DENIED");
        ValidatePage(query.Page, query.PageSize, 100);
        if (query.BeforeId is <= 0 || query.AfterId is <= 0 || (query.BeforeId.HasValue && query.AfterId.HasValue) || ((query.BeforeId.HasValue || query.AfterId.HasValue) && query.Page != 1)) throw Fail(400, "Cursor không hợp lệ.", "INVALID_QUERY");
        var rows = Messages().AsNoTracking().Where(m => m.ConversationId == conversationId); var total = await rows.CountAsync();
        if (query.BeforeId.HasValue) rows = rows.Where(m => m.Id < query.BeforeId);
        if (query.AfterId.HasValue) rows = rows.Where(m => m.Id > query.AfterId);
        // REST cursor pages use stable IDs; retain legacy chronological pagination when LatestFirst is false.
        var ordered = query.LatestFirst && !query.AfterId.HasValue ? rows.OrderByDescending(m => m.Id) : rows.OrderBy(m => m.Id);
        var items = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize + 1).ToListAsync();
        return new() { Items = items.Take(query.PageSize).Select(MessageDto).ToList(), TotalCount = total, Page = query.Page, PageSize = query.PageSize, HasMore = items.Count > query.PageSize };
    }
    public Task<ChatMessageDto> SendTextMessageAsync(int conversationId, int senderId, string content, Guid? clientRequestId = null)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000) throw Fail(400, "Tin nhắn cần từ 1 đến 2000 ký tự.", "INVALID_MESSAGE");
        return SendAsync(conversationId, senderId, content.Trim(), null, clientRequestId);
    }
    public Task<ChatMessageDto> SendProductCardAsync(int conversationId, int senderId, int productId, Guid? clientRequestId = null) => SendAsync(conversationId, senderId, "", productId, clientRequestId);
    private async Task<ChatMessageDto> SendAsync(int conversationId, int senderId, string content, int? productId, Guid? requestId)
    {
        await UserAsync(senderId); if (requestId == Guid.Empty) throw Fail(400, "Mã gửi tin không hợp lệ.", "INVALID_REQUEST_ID");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        // All senders serialize on the conversation; message + inbox timestamp commit together.
        var conversation = await _context.Conversations.FromSqlInterpolated($"SELECT * FROM [forum].[Conversations] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {conversationId}").SingleOrDefaultAsync() ?? throw Fail(404, "Không tìm thấy hội thoại.", "CONVERSATION_NOT_FOUND");
        if (conversation.BuyerId != senderId && conversation.SellerId != senderId) throw Fail(403, "Bạn không có quyền gửi tin.", "CHAT_ACCESS_DENIED");
        var kind = productId.HasValue ? "PRODUCT_CARD" : "TEXT";
        if (requestId.HasValue) {
            var existing = await Messages().SingleOrDefaultAsync(m => m.ConversationId == conversationId && m.SenderId == senderId && m.ClientRequestId == requestId);
            if (existing != null) { if (existing.MessageType != kind || existing.ProductId != productId || (!productId.HasValue && existing.Content != content)) throw Fail(409, "Mã gửi tin đã dùng cho nội dung khác.", "REQUEST_ID_REUSED"); await tx.CommitAsync(); return MessageDto(existing); }
        }
        if (conversation.IsClosed) throw Fail(409, "Hội thoại đã đóng.", "CONVERSATION_CLOSED");
        if (await _context.Users.AnyAsync(u => (u.Id == conversation.BuyerId || u.Id == conversation.SellerId) && u.Role == "Banned")) throw Fail(403, "Thành viên hội thoại không còn được phép nhận tin.", "CHAT_ACCESS_DENIED");
        if (productId.HasValue) { var product = await _context.Products.Include(p => p.Store).ThenInclude(s => s.Owner).ThenInclude(u => u.MerchantProfile).SingleOrDefaultAsync(p => p.Id == productId); if (product == null || product.Store.OwnerId != conversation.SellerId || !Public(product)) throw Fail(404, "Sản phẩm không thuộc shop hoặc không công khai.", "PRODUCT_NOT_FOUND"); content = $"[Product: {product.Title}]"; }
        var message = new ChatMessage { ConversationId = conversationId, SenderId = senderId, Content = content, ProductId = productId, MessageType = kind, ClientRequestId = requestId };
        _context.ChatMessages.Add(message); conversation.LastMessageAt = message.SentAt; conversation.UpdatedAt = message.SentAt;
        await Doosii.DAL.Data.UploadedImageGuard.SaveChangesAsync(_context); await tx.CommitAsync(); return MessageDto(await Messages().SingleAsync(m => m.Id == message.Id));
    }
    public async Task<ReadStatusDto> MarkMessagesAsReadAsync(int conversationId, int userId, int? upToId = null)
    {
        if (!await IsMemberAsync(conversationId, userId)) throw Fail(403, "Bạn không có quyền đọc hội thoại này.", "CHAT_ACCESS_DENIED");
        if (upToId is <= 0) throw Fail(400, "Mốc đọc không hợp lệ.", "INVALID_QUERY");
        var rows = _context.ChatMessages.Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead && !m.IsDeleted);
        var boundary = await _context.ChatMessages.Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsDeleted && (!upToId.HasValue || m.Id <= upToId)).MaxAsync(m => (int?)m.Id) ?? 0;
        var marked = await rows.Where(m => m.Id <= boundary).ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        return new() { ConversationId = conversationId, ReaderId = userId, UpToId = boundary, MarkedCount = marked, UnreadCount = await rows.CountAsync() };
    }
    public async Task<ProductCardDto?> GetProductCardAsync(int productId) { var p = await _context.Products.AsNoTracking().Include(p => p.Images).Include(p => p.Store).ThenInclude(s => s.Owner).ThenInclude(u => u.MerchantProfile).SingleOrDefaultAsync(p => p.Id == productId); return p == null ? null : Card(p); }
    private static ProductCardDto Card(Product p) => new() { ProductId = p.Id, Title = p.Title, Price = p.Price, Status = p.Status, ThumbnailUrl = p.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(), StoreId = p.StoreId, StoreName = p.Store.Name, IsPublic = Public(p) };
    private async Task<ConversationDto> ConversationDtoAsync(Conversation c, int userId)
    {
        var last = await Messages().AsNoTracking().Where(m => m.ConversationId == c.Id && !m.IsDeleted).OrderByDescending(m => m.Id).FirstOrDefaultAsync();
        var shop = await _context.Stores.AsNoTracking().Where(s => s.OwnerId == c.SellerId && (!c.ProductId.HasValue || s.Id == c.Product!.StoreId)).OrderBy(s => s.Id).Select(s => new { s.Id, s.Name }).FirstOrDefaultAsync();
        var readStatuses = new List<ReadStatusDto>();
        foreach (var reader in new[] { c.BuyerId, c.SellerId }) {
            var boundary = await _context.ChatMessages.Where(m => m.ConversationId == c.Id && m.SenderId != reader && m.IsRead && !m.IsDeleted).MaxAsync(m => (int?)m.Id) ?? 0;
            readStatuses.Add(new() { ConversationId = c.Id, ReaderId = reader, UpToId = boundary });
        }
        return new() { ReadStatuses = readStatuses, Id = c.Id, BuyerId = c.BuyerId, SellerId = c.SellerId, ProductId = c.ProductId, IsClosed = c.IsClosed, CreatedAt = DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc), LastMessageAt = DateTime.SpecifyKind(c.LastMessageAt, DateTimeKind.Utc), StoreId = shop?.Id, StoreName = shop?.Name ?? "", Buyer = new() { Id = c.Buyer.Id, FullName = c.Buyer.FullName, AvatarUrl = c.Buyer.AvatarUrl }, Seller = new() { Id = c.Seller.Id, FullName = c.Seller.FullName, AvatarUrl = c.Seller.AvatarUrl }, Product = c.Product == null ? null : Card(c.Product), LastMessage = last == null ? null : MessageDto(last), UnreadCount = await _context.ChatMessages.CountAsync(m => m.ConversationId == c.Id && m.SenderId != userId && !m.IsRead && !m.IsDeleted) };
    }
    private static ChatMessageDto MessageDto(ChatMessage m) => new() { Id = m.Id, ConversationId = m.ConversationId, SenderId = m.SenderId, SenderName = m.Sender?.FullName ?? "", SenderAvatar = m.Sender?.AvatarUrl, Content = m.IsDeleted ? "Tin nhắn đã được xóa" : m.Content, ProductId = m.ProductId, MessageType = m.MessageType, SentAt = DateTime.SpecifyKind(m.SentAt, DateTimeKind.Utc), IsRead = m.IsRead, IsDeleted = m.IsDeleted, ClientRequestId = m.ClientRequestId, ProductCard = !m.IsDeleted && m.Product != null && m.MessageType == "PRODUCT_CARD" ? Card(m.Product) : null };
}
