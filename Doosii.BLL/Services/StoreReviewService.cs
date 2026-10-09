using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;

namespace Doosii.BLL.Services
{
    public class StoreReviewService : IStoreReviewService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StoreReviewService> _logger;

        public StoreReviewService(AppDbContext context, ILogger<StoreReviewService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<StoreReviewDto> CreateReviewAsync(int userId, int storeId, CreateStoreReviewRequest request)
        {
            // 1. Validate Rating
            if (request.Rating < 1 || request.Rating > 5)
            {
                throw new ServiceException(400, "Rating must be between 1 and 5", "INVALID_RATING");
            }

            // 2. Validate Images (toi da 3 anh)
            if (request.Images != null && request.Images.Count > 3)
            {
                throw new ServiceException(400, "A maximum of 3 images is allowed", "MAX_3_IMAGES");
            }

            // 3. Validate Comment length
            if (request.Comment != null && request.Comment.Length > 2000)
            {
                throw new ServiceException(400, "Comment cannot exceed 2000 characters", "COMMENT_TOO_LONG");
            }

            // 4. Kiem tra Store ton tai va dang hoat dong
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null)
            {
                throw new ServiceException(404, "Store not found", "STORE_NOT_FOUND");
            }

            if (!store.IsActive)
            {
                throw new ServiceException(400, "Store is inactive", "STORE_NOT_ACTIVE");
            }

            // 5. User khong duoc danh gia store cua chinh minh
            if (store.OwnerId == userId)
            {
                throw new ServiceException(400, "User cannot review their own store", "CANNOT_REVIEW_OWN_STORE");
            }

            // 6. User khong duoc review lan thu 2 cho cung mot store
            var alreadyReviewed = await _context.StoreReviews
                .AnyAsync(r => r.StoreId == storeId && r.UserId == userId);
            if (alreadyReviewed)
            {
                throw new ServiceException(400, "User has already reviewed this store", "REVIEW_ALREADY_EXISTS");
            }

            // 7. Kiem tra user da hoan thanh giao dich mua hang tai store nay (COMPLETED_RELEASED)
            int? matchedOrderId = null;
            if (request.OrderId.HasValue)
            {
                var specificOrder = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == request.OrderId.Value && o.BuyerId == userId);

                if (specificOrder == null || specificOrder.Status != OrderStatus.CompletedReleased)
                {
                    throw new ServiceException(400, "Eligible completed order not found", "NO_COMPLETED_ORDER");
                }

                var itemPIds = specificOrder.Items.Select(i => i.ProductId).ToList();
                var containsStoreProduct = await _context.Products
                    .AnyAsync(p => itemPIds.Contains(p.Id) && p.StoreId == storeId);

                if (!containsStoreProduct)
                {
                    throw new ServiceException(400, "The specified order does not contain products from this store", "NO_COMPLETED_ORDER");
                }

                matchedOrderId = specificOrder.Id;
            }
            else
            {
                var completedOrders = await _context.Orders
                    .Include(o => o.Items)
                    .Where(o => o.BuyerId == userId && o.Status == OrderStatus.CompletedReleased)
                    .OrderByDescending(o => o.CompletedAt ?? o.CreatedAt)
                    .ToListAsync();

                foreach (var order in completedOrders)
                {
                    var pIds = order.Items.Select(i => i.ProductId).ToList();
                    var belongs = await _context.Products
                        .AnyAsync(p => pIds.Contains(p.Id) && p.StoreId == storeId);

                    if (belongs)
                    {
                        matchedOrderId = order.Id;
                        break;
                    }
                }

                if (matchedOrderId == null)
                {
                    throw new ServiceException(400, "Only users who have completed an order with this store can review", "NO_COMPLETED_ORDER");
                }
            }

            // 8. Luu StoreReview
            var review = new StoreReview
            {
                StoreId = storeId,
                UserId = userId,
                OrderId = matchedOrderId,
                Rating = request.Rating,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                ImagesJson = request.Images != null && request.Images.Any() ? JsonSerializer.Serialize(request.Images) : null,
                CreatedAt = DateTime.UtcNow
            };

            _context.StoreReviews.Add(review);
            await _context.SaveChangesAsync();

            // 9. Cap nhat Store.RatingCount va Store.RatingAverage
            await RecalculateStoreRatingAsync(storeId);

            // 10. Tra ve DTO
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            return new StoreReviewDto
            {
                ReviewId = review.Id,
                UserId = userId,
                UserDisplayName = user?.FullName ?? string.Empty,
                UserAvatarUrl = user?.AvatarUrl,
                Rating = review.Rating,
                Comment = review.Comment,
                Images = request.Images ?? new List<string>(),
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            };
        }

        public async Task<StoreReviewDto> UpdateReviewAsync(int userId, int storeId, int reviewId, UpdateStoreReviewRequest request)
        {
            // 1. Validate Rating
            if (request.Rating < 1 || request.Rating > 5)
            {
                throw new ServiceException(400, "Rating must be between 1 and 5", "INVALID_RATING");
            }

            // 2. Validate Images
            if (request.Images != null && request.Images.Count > 3)
            {
                throw new ServiceException(400, "A maximum of 3 images is allowed", "MAX_3_IMAGES");
            }

            // 3. Validate Comment
            if (request.Comment != null && request.Comment.Length > 2000)
            {
                throw new ServiceException(400, "Comment cannot exceed 2000 characters", "COMMENT_TOO_LONG");
            }

            // 4. Kiem tra Store
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null)
            {
                throw new ServiceException(404, "Store not found", "STORE_NOT_FOUND");
            }

            // 5. Kiem tra Review
            var review = await _context.StoreReviews
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.StoreId == storeId);

            if (review == null)
            {
                throw new ServiceException(404, "Review not found", "REVIEW_NOT_FOUND");
            }

            // 6. Kiem tra quyen so huu (Chi chinh user moi duoc sua)
            if (review.UserId != userId)
            {
                throw new ServiceException(403, "You are not authorized to edit this review", "FORBIDDEN");
            }

            // 7. Cap nhat cac truong cho phep (khong doi StoreId, UserId)
            review.Rating = request.Rating;
            review.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            review.ImagesJson = request.Images != null && request.Images.Any() ? JsonSerializer.Serialize(request.Images) : null;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // 8. Tinh lai Store.RatingAverage
            await RecalculateStoreRatingAsync(storeId);

            var images = !string.IsNullOrEmpty(review.ImagesJson)
                ? JsonSerializer.Deserialize<List<string>>(review.ImagesJson) ?? new List<string>()
                : new List<string>();

            return new StoreReviewDto
            {
                ReviewId = review.Id,
                UserId = review.UserId,
                UserDisplayName = review.User?.FullName ?? string.Empty,
                UserAvatarUrl = review.User?.AvatarUrl,
                Rating = review.Rating,
                Comment = review.Comment,
                Images = images,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            };
        }

        public async Task<StoreReviewsSummaryDto> GetReviewsAsync(int storeId, StoreReviewQuery query)
        {
            var store = await _context.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null)
            {
                throw new ServiceException(404, "Store not found", "STORE_NOT_FOUND");
            }

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 10 : (query.PageSize > 50 ? 50 : query.PageSize);

            var queryable = _context.StoreReviews
                .AsNoTracking()
                .Where(r => r.StoreId == storeId);

            var totalReviews = await queryable.CountAsync();

            var sortBy = (query.SortBy ?? "newest").Trim().ToLower();
            if (sortBy == "highest")
            {
                queryable = queryable.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt);
            }
            else if (sortBy == "lowest")
            {
                queryable = queryable.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt);
            }
            else // newest
            {
                queryable = queryable.OrderByDescending(r => r.CreatedAt);
            }

            var items = await queryable
                .Include(r => r.User)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var reviewDtos = items.Select(r => new StoreReviewDto
            {
                ReviewId = r.Id,
                UserId = r.UserId,
                UserDisplayName = r.User != null ? r.User.FullName : string.Empty,
                UserAvatarUrl = r.User?.AvatarUrl,
                Rating = r.Rating,
                Comment = r.Comment,
                Images = !string.IsNullOrEmpty(r.ImagesJson)
                    ? JsonSerializer.Deserialize<List<string>>(r.ImagesJson) ?? new List<string>()
                    : new List<string>(),
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return new StoreReviewsSummaryDto
            {
                StoreId = storeId,
                RatingAverage = store.RatingAverage,
                TotalReviews = totalReviews,
                Page = page,
                PageSize = pageSize,
                TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalReviews / pageSize) : 0,
                Items = reviewDtos
            };
        }

        public async Task DeleteReviewAsync(int userId, bool isAdmin, int storeId, int reviewId)
        {
            var review = await _context.StoreReviews.FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review == null)
            {
                throw new ServiceException(404, "Review not found", "REVIEW_NOT_FOUND");
            }

            if (review.StoreId != storeId)
            {
                throw new ServiceException(400, "Review does not belong to this store", "REVIEW_STORE_MISMATCH");
            }

            // Chi nguoi tao review hoac Admin duoc xoa
            if (review.UserId != userId && !isAdmin)
            {
                throw new ServiceException(403, "You are not authorized to delete this review", "FORBIDDEN");
            }

            _context.StoreReviews.Remove(review);
            await _context.SaveChangesAsync();

            // Tinh lai RatingCount va RatingAverage cho Store
            await RecalculateStoreRatingAsync(storeId);
        }

        private async Task RecalculateStoreRatingAsync(int storeId)
        {
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId);
            if (store == null) return;

            var stats = await _context.StoreReviews
                .Where(r => r.StoreId == storeId)
                .GroupBy(r => r.StoreId)
                .Select(g => new { Count = g.Count(), Avg = g.Average(r => (double)r.Rating) })
                .FirstOrDefaultAsync();

            store.RatingCount = stats?.Count ?? 0;
            store.RatingAverage = stats != null ? Math.Round((decimal)stats.Avg, 2) : 0m;
            await _context.SaveChangesAsync();
        }
    }
}
