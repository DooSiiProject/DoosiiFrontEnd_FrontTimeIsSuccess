using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Forum;

namespace Doosii.BLL.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;

        public ChatService(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================================
        // CONVERSATION
        // =====================================================================

        public async Task<ConversationDto> GetOrCreateConversationAsync(int buyerId, int sellerId, int? productId)
        {
            if (buyerId == sellerId)
            {
                throw new ServiceException(400, "You cannot chat with yourself", "CANNOT_CHAT_WITH_SELF");
            }

            // Kiem tra seller ton tai
            var sellerExists = await _context.Users.AnyAsync(u => u.Id == sellerId);
            if (!sellerExists)
            {
                throw new ServiceException(404, "Seller not found", "CONVERSATION_NOT_FOUND");
            }

            // Neu co productId: kiem tra product ton tai va seller la owner
            if (productId.HasValue)
            {
                var product = await _context.Products
                    .Include(p => p.Store)
                    .FirstOrDefaultAsync(p => p.Id == productId.Value);

                if (product == null)
                {
                    throw new ServiceException(404, "Product not found", "PRODUCT_NOT_FOUND");
                }

                if (product.Store.OwnerId != sellerId)
                {
                    throw new ServiceException(400, "Seller is not the owner of this product", "PRODUCT_NOT_FOUND");
                }
            }

            // Tim conversation da ton tai (buyer, seller, product)
            var existing = await _context.Conversations
                .Include(c => c.Buyer)
                .Include(c => c.Seller)
                .Include(c => c.Product)
                    .ThenInclude(p => p != null ? p.Store : null)
                .Include(c => c.Product)
                    .ThenInclude(p => p != null ? p.Images : null)
                .FirstOrDefaultAsync(c =>
                    c.BuyerId == buyerId &&
                    c.SellerId == sellerId &&
                    c.ProductId == productId);

            if (existing != null)
            {
                return await BuildConversationDtoAsync(existing, buyerId);
            }

            // Tao moi
            var conversation = new Conversation
            {
                BuyerId = buyerId,
                SellerId = sellerId,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            // Reload voi navigation
            var created = await _context.Conversations
                .Include(c => c.Buyer)
                .Include(c => c.Seller)
                .Include(c => c.Product)
                    .ThenInclude(p => p != null ? p.Store : null)
                .Include(c => c.Product)
                    .ThenInclude(p => p != null ? p.Images : null)
                .FirstAsync(c => c.Id == conversation.Id);

            return await BuildConversationDtoAsync(created, buyerId);
        }

        public async Task<PagedResult<ConversationDto>> GetConversationsAsync(int userId, GetConversationsQuery query)
        {
            var queryable = _context.Conversations
                .Where(c => c.BuyerId == userId || c.SellerId == userId)
                .AsNoTracking();

            var totalCount = await queryable.CountAsync();

            int page = query.Page < 1 ? 1 : query.Page;
            int pageSize = query.PageSize < 1 ? 20 : (query.PageSize > 50 ? 50 : query.PageSize);

            var conversations = await queryable
                .Include(c => c.Buyer)
                .Include(c => c.Seller)
                .Include(c => c.Product)
                    .ThenInclude(p => p != null ? p.Store : null)
                .Include(c => c.Product)
                    .ThenInclude(p => p != null ? p.Images : null)
                .OrderByDescending(c => c.LastMessageAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = new List<ConversationDto>();
            foreach (var conv in conversations)
            {
                items.Add(await BuildConversationDtoAsync(conv, userId));
            }

            return new PagedResult<ConversationDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // =====================================================================
        // MESSAGES
        // =====================================================================

        public async Task<PagedResult<ChatMessageDto>> GetMessagesAsync(int conversationId, int userId, GetMessagesQuery query)
        {
            var isMember = await IsMemberAsync(conversationId, userId);
            if (!isMember)
            {
                throw new ServiceException(403, "You are not a member of this conversation", "CHAT_ACCESS_DENIED");
            }

            int page = query.Page < 1 ? 1 : query.Page;
            int pageSize = query.PageSize < 1 ? 30 : (query.PageSize > 100 ? 100 : query.PageSize);

            var total = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .AsNoTracking()
                .CountAsync();

            var messages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .Include(m => m.Sender)
                .Include(m => m.Product)
                    .ThenInclude(p => p != null ? p.Store : null)
                .Include(m => m.Product)
                    .ThenInclude(p => p != null ? p.Images : null)
                .OrderBy(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            var items = messages.Select(m => MapToMessageDto(m)).ToList();

            return new PagedResult<ChatMessageDto>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<ChatMessageDto> SendTextMessageAsync(int conversationId, int senderId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ServiceException(400, "Message content cannot be empty", "INVALID_MESSAGE");
            }

            if (content.Length > 2000)
            {
                throw new ServiceException(400, "Message content cannot exceed 2000 characters", "INVALID_MESSAGE");
            }

            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
            if (conversation == null)
            {
                throw new ServiceException(404, "Conversation not found", "CONVERSATION_NOT_FOUND");
            }

            if (conversation.IsClosed)
            {
                throw new ServiceException(400, "This conversation is closed", "CONVERSATION_CLOSED");
            }

            if (conversation.BuyerId != senderId && conversation.SellerId != senderId)
            {
                throw new ServiceException(403, "You are not a member of this conversation", "CHAT_ACCESS_DENIED");
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var message = new ChatMessage
                {
                    ConversationId = conversationId,
                    SenderId = senderId,
                    Content = content.Trim(),
                    MessageType = "TEXT",
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    IsDeleted = false
                };

                _context.ChatMessages.Add(message);

                conversation.LastMessageAt = message.SentAt;
                conversation.UpdatedAt = message.SentAt;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // Reload with navigation for DTO
                var saved = await _context.ChatMessages
                    .Include(m => m.Sender)
                    .FirstAsync(m => m.Id == message.Id);

                return MapToMessageDto(saved);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<ChatMessageDto> SendProductCardAsync(int conversationId, int senderId, int productId)
        {
            var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
            if (conversation == null)
            {
                throw new ServiceException(404, "Conversation not found", "CONVERSATION_NOT_FOUND");
            }

            if (conversation.IsClosed)
            {
                throw new ServiceException(400, "This conversation is closed", "CONVERSATION_CLOSED");
            }

            if (conversation.BuyerId != senderId && conversation.SellerId != senderId)
            {
                throw new ServiceException(403, "You are not a member of this conversation", "CHAT_ACCESS_DENIED");
            }

            var product = await _context.Products
                .Include(p => p.Store)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                throw new ServiceException(404, "Product not found", "PRODUCT_NOT_FOUND");
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var message = new ChatMessage
                {
                    ConversationId = conversationId,
                    SenderId = senderId,
                    Content = $"[Product: {product.Title}]",
                    ProductId = productId,
                    MessageType = "PRODUCT_CARD",
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    IsDeleted = false
                };

                _context.ChatMessages.Add(message);

                conversation.LastMessageAt = message.SentAt;
                conversation.UpdatedAt = message.SentAt;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                var saved = await _context.ChatMessages
                    .Include(m => m.Sender)
                    .Include(m => m.Product)
                        .ThenInclude(p => p != null ? p.Store : null)
                    .Include(m => m.Product)
                        .ThenInclude(p => p != null ? p.Images : null)
                    .FirstAsync(m => m.Id == message.Id);

                return MapToMessageDto(saved);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<ReadStatusDto> MarkMessagesAsReadAsync(int conversationId, int userId)
        {
            var isMember = await IsMemberAsync(conversationId, userId);
            if (!isMember)
            {
                throw new ServiceException(403, "You are not a member of this conversation", "CHAT_ACCESS_DENIED");
            }

            // Danh dau message cua nguoi kia la da doc
            var unreadMessages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId
                         && m.SenderId != userId
                         && !m.IsRead
                         && !m.IsDeleted)
                .ToListAsync();

            int markedCount = unreadMessages.Count;
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
            }

            if (markedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            int remainingUnread = await _context.ChatMessages
                .CountAsync(m => m.ConversationId == conversationId
                              && m.SenderId != userId
                              && !m.IsRead
                              && !m.IsDeleted);

            return new ReadStatusDto
            {
                ConversationId = conversationId,
                UnreadCount = remainingUnread,
                MarkedCount = markedCount
            };
        }

        public async Task<bool> IsMemberAsync(int conversationId, int userId)
        {
            return await _context.Conversations
                .AnyAsync(c => c.Id == conversationId && (c.BuyerId == userId || c.SellerId == userId));
        }

        public async Task<ProductCardDto?> GetProductCardAsync(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Store)
                .Include(p => p.Images)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null) return null;

            return new ProductCardDto
            {
                ProductId = product.Id,
                Title = product.Title,
                Price = product.Price,
                Status = product.Status,
                ThumbnailUrl = product.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
                StoreId = product.StoreId,
                StoreName = product.Store.Name
            };
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        private async Task<ConversationDto> BuildConversationDtoAsync(Conversation conversation, int currentUserId)
        {
            // Last message preview
            var lastMsg = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.ConversationId == conversation.Id && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();

            // Unread count: messages sent by the other party that are unread
            var unreadCount = await _context.ChatMessages
                .CountAsync(m => m.ConversationId == conversation.Id
                              && m.SenderId != currentUserId
                              && !m.IsRead
                              && !m.IsDeleted);

            ProductCardDto? productCard = null;
            if (conversation.Product != null)
            {
                productCard = new ProductCardDto
                {
                    ProductId = conversation.Product.Id,
                    Title = conversation.Product.Title,
                    Price = conversation.Product.Price,
                    Status = conversation.Product.Status,
                    ThumbnailUrl = conversation.Product.Images
                        .OrderBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault(),
                    StoreId = conversation.Product.StoreId,
                    StoreName = conversation.Product.Store?.Name ?? string.Empty
                };
            }

            return new ConversationDto
            {
                Id = conversation.Id,
                BuyerId = conversation.BuyerId,
                SellerId = conversation.SellerId,
                ProductId = conversation.ProductId,
                IsClosed = conversation.IsClosed,
                CreatedAt = conversation.CreatedAt,
                LastMessageAt = conversation.LastMessageAt,
                Buyer = new UserSummaryDto
                {
                    Id = conversation.Buyer.Id,
                    FullName = conversation.Buyer.FullName,
                    AvatarUrl = conversation.Buyer.AvatarUrl
                },
                Seller = new UserSummaryDto
                {
                    Id = conversation.Seller.Id,
                    FullName = conversation.Seller.FullName,
                    AvatarUrl = conversation.Seller.AvatarUrl
                },
                Product = productCard,
                LastMessage = lastMsg != null ? MapToMessageDto(lastMsg) : null,
                UnreadCount = unreadCount
            };
        }

        private static ChatMessageDto MapToMessageDto(ChatMessage m)
        {
            ProductCardDto? productCard = null;
            if (m.MessageType == "PRODUCT_CARD" && m.Product != null)
            {
                productCard = new ProductCardDto
                {
                    ProductId = m.Product.Id,
                    Title = m.Product.Title,
                    Price = m.Product.Price,
                    Status = m.Product.Status,
                    ThumbnailUrl = m.Product.Images?
                        .OrderBy(i => i.DisplayOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault(),
                    StoreId = m.Product.StoreId,
                    StoreName = m.Product.Store?.Name ?? string.Empty
                };
            }

            return new ChatMessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender?.FullName ?? "Unknown",
                SenderAvatar = m.Sender?.AvatarUrl,
                Content = m.IsDeleted ? "[This message was deleted]" : m.Content,
                ProductId = m.ProductId,
                MessageType = m.MessageType,
                SentAt = m.SentAt,
                IsRead = m.IsRead,
                IsDeleted = m.IsDeleted,
                ProductCard = productCard
            };
        }
    }
}
