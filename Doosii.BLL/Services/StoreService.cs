using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Helpers;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models.Store;

namespace Doosii.BLL.Services
{
    public class StoreService : IStoreService
    {
        private readonly AppDbContext _context;

        public StoreService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<StoreDto> CreateStoreAsync(int userId, CreateStoreRequest request)
        {
            var user = await _context.Users
                .Include(u => u.MerchantProfile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException("Khong tim thay nguoi dung.");

            if (user.Role != "Seller")
                throw new UnauthorizedAccessException("Chi nguoi dung co vai tro Seller moi duoc tao cua hang.");

            if (user.MerchantProfile == null || user.MerchantProfile.KycStatus != "APPROVED")
                throw new UnauthorizedAccessException("Tai khoan chua duoc duyet KYC. Vui long cho Admin xac nhan.");

            var existingStore = await _context.Stores.FirstOrDefaultAsync(s => s.OwnerId == userId);
            if (existingStore != null)
                throw new InvalidOperationException("Ban da co cua hang. Moi Seller chi duoc tao mot cua hang.");

            var store = new Store
            {
                OwnerId = userId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Phone = request.Phone?.Trim(),
                Address = request.Address?.Trim(),
                OpeningHours = request.OpeningHours?.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Stores.Add(store);
            await _context.SaveChangesAsync();

            if (request.Latitude.HasValue || request.Longitude.HasValue || !string.IsNullOrWhiteSpace(request.Address))
            {
                var location = new StoreLocation
                {
                    StoreId = store.Id,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    Address = request.Address?.Trim()
                };
                _context.StoreLocations.Add(location);
                await _context.SaveChangesAsync();
            }

            return await GetStoreAsync(store.Id);
        }

        public async Task<StoreDto> UpdateStoreAsync(int userId, int storeId, UpdateStoreRequest request)
        {
            var store = await _context.Stores
                .Include(s => s.Locations)
                .FirstOrDefaultAsync(s => s.Id == storeId);

            if (store == null)
                throw new KeyNotFoundException("Khong tim thay cua hang.");

            if (store.OwnerId != userId)
                throw new UnauthorizedAccessException("Ban khong co quyen chinh sua cua hang nay.");

            store.Name = request.Name.Trim();
            store.Description = request.Description?.Trim();
            store.Phone = request.Phone?.Trim();
            store.Address = request.Address?.Trim();
            store.OpeningHours = request.OpeningHours?.Trim();
            store.Latitude = request.Latitude;
            store.Longitude = request.Longitude;
            store.UpdatedAt = DateTime.UtcNow;

            var location = store.Locations.FirstOrDefault();
            if (location == null)
            {
                if (request.Latitude.HasValue || request.Longitude.HasValue)
                {
                    location = new StoreLocation { StoreId = store.Id };
                    _context.StoreLocations.Add(location);
                }
            }

            if (location != null)
            {
                location.Latitude = request.Latitude;
                location.Longitude = request.Longitude;
                location.Address = request.Address?.Trim();
            }

            await _context.SaveChangesAsync();
            return await GetStoreAsync(store.Id);
        }

        public async Task<StoreDto> GetStoreAsync(int storeId)
        {
            var store = await _context.Stores
                .Include(s => s.Locations)
                .FirstOrDefaultAsync(s => s.Id == storeId && s.IsActive);

            if (store == null)
                throw new KeyNotFoundException("Khong tim thay cua hang hoac cua hang khong hoat dong.");

            return MapToStoreDto(store);
        }

        public async Task<PagedResponse<NearbyStoreDto>> GetNearbyStoresAsync(NearbyStoreQuery query)
        {
            var lat = query.Lat!.Value;
            var lng = query.Lng!.Value;
            var radius = query.RadiusKm.GetValueOrDefault(5);

            // 1. Tinh Bounding Box de loc so bo truc tiep tren SQL Server
            var (minLat, maxLat, minLng, maxLng) = GeoCalculator.GetBoundingBox(lat, lng, radius);

            var candidateStores = await _context.Stores
                .AsNoTracking()
                .Include(s => s.Locations)
                .Where(s => s.IsActive &&
                    (
                        (s.Latitude != null && s.Latitude >= minLat && s.Latitude <= maxLat &&
                         s.Longitude != null && s.Longitude >= minLng && s.Longitude <= maxLng)
                        ||
                        (s.Locations.Any(l => l.Latitude != null && l.Latitude >= minLat && l.Latitude <= maxLat &&
                                              l.Longitude != null && l.Longitude >= minLng && l.Longitude <= maxLng))
                    )
                )
                .ToListAsync();

            // 2. Tinh toan khoang cach Haversine chinh xac trong bo nho
            var nearbyList = new List<(Store Store, double? Lat, double? Lng, double DistanceKm)>();

            foreach (var s in candidateStores)
            {
                var storeLat = s.Latitude ?? s.Locations.Select(l => l.Latitude).FirstOrDefault();
                var storeLng = s.Longitude ?? s.Locations.Select(l => l.Longitude).FirstOrDefault();

                if (!storeLat.HasValue || !storeLng.HasValue)
                    continue;

                var dist = GeoCalculator.CalculateDistanceKm(lat, lng, storeLat.Value, storeLng.Value);
                if (dist <= radius)
                {
                    nearbyList.Add((s, storeLat, storeLng, Math.Round(dist, 2)));
                }
            }

            // 3. Sap xep theo khoang cach gan nhat
            var sortedList = nearbyList.OrderBy(x => x.DistanceKm).ToList();
            var totalItems = sortedList.Count;

            // 4. Phan trang
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
            var paged = sortedList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var items = paged.Select(x => MapToNearbyStoreDto(x.Store, x.Lat, x.Lng, x.DistanceKm)).ToList();

            return new PagedResponse<NearbyStoreDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }

        public async Task<PagedResponse<NearbyStoreDto>> GetStoresAsync(StoreFilterQuery query)
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

            var queryable = _context.Stores
                .AsNoTracking()
                .Include(s => s.Locations)
                .Where(s => s.IsActive);

            // 1. Loc theo Style tu Product.StyleTags (AND)
            if (!string.IsNullOrWhiteSpace(query.Style))
            {
                var styleTrim = query.Style.Trim();
                queryable = queryable.Where(s => s.Products.Any(p => p.StyleTags != null && p.StyleTags.Contains(styleTrim)));
            }

            // 2. Loc theo khoang gia san pham (AND)
            if (query.MinPrice.HasValue && query.MaxPrice.HasValue)
            {
                queryable = queryable.Where(s => s.Products.Any(p => p.Price >= query.MinPrice.Value && p.Price <= query.MaxPrice.Value));
            }
            else if (query.MinPrice.HasValue)
            {
                queryable = queryable.Where(s => s.Products.Any(p => p.Price >= query.MinPrice.Value));
            }
            else if (query.MaxPrice.HasValue)
            {
                queryable = queryable.Where(s => s.Products.Any(p => p.Price <= query.MaxPrice.Value));
            }

            // 3. Loc theo Rating toi thieu (AND)
            if (query.MinRating.HasValue)
            {
                queryable = queryable.Where(s => s.RatingAverage >= query.MinRating.Value);
            }

            // 4. Sap xep
            var sortBy = (query.SortBy ?? "newest").Trim().ToLower();

            if (sortBy == "distance")
            {
                var lat = query.Lat!.Value;
                var lng = query.Lng!.Value;

                var filteredStores = await queryable.ToListAsync();
                var withDistance = filteredStores.Select(s =>
                {
                    var storeLat = s.Latitude ?? s.Locations.Select(l => l.Latitude).FirstOrDefault();
                    var storeLng = s.Longitude ?? s.Locations.Select(l => l.Longitude).FirstOrDefault();
                    double? dist = null;

                    if (storeLat.HasValue && storeLng.HasValue)
                    {
                        dist = Math.Round(GeoCalculator.CalculateDistanceKm(lat, lng, storeLat.Value, storeLng.Value), 2);
                    }

                    return new { Store = s, Lat = storeLat, Lng = storeLng, DistanceKm = dist };
                })
                .OrderBy(x => x.DistanceKm.HasValue ? 0 : 1)
                .ThenBy(x => x.DistanceKm)
                .ThenByDescending(x => x.Store.CreatedAt)
                .ToList();

                var totalItems = withDistance.Count;
                var paged = withDistance.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                var items = paged.Select(x => MapToNearbyStoreDto(x.Store, x.Lat, x.Lng, x.DistanceKm)).ToList();

                return new PagedResponse<NearbyStoreDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems
                };
            }
            else if (sortBy == "rating")
            {
                queryable = queryable.OrderByDescending(s => s.RatingAverage)
                                     .ThenByDescending(s => s.RatingCount)
                                     .ThenByDescending(s => s.Id);
            }
            else // "newest" hoac mac dinh
            {
                queryable = queryable.OrderByDescending(s => s.CreatedAt)
                                     .ThenByDescending(s => s.Id);
            }

            var count = await queryable.CountAsync();
            var pagedStores = await queryable.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var pagedDtos = pagedStores.Select(s =>
            {
                var storeLat = s.Latitude ?? s.Locations.Select(l => l.Latitude).FirstOrDefault();
                var storeLng = s.Longitude ?? s.Locations.Select(l => l.Longitude).FirstOrDefault();
                double? dist = null;

                if (query.Lat.HasValue && query.Lng.HasValue && storeLat.HasValue && storeLng.HasValue)
                {
                    dist = Math.Round(GeoCalculator.CalculateDistanceKm(query.Lat.Value, query.Lng.Value, storeLat.Value, storeLng.Value), 2);
                }

                return MapToNearbyStoreDto(s, storeLat, storeLng, dist);
            }).ToList();

            return new PagedResponse<NearbyStoreDto>
            {
                Items = pagedDtos,
                Page = page,
                PageSize = pageSize,
                TotalItems = count
            };
        }

        private static NearbyStoreDto MapToNearbyStoreDto(Store s, double? lat, double? lng, double? distanceKm)
        {
            return new NearbyStoreDto
            {
                StoreId = s.Id,
                StoreName = s.Name,
                Description = s.Description,
                Address = s.Address ?? s.Locations.Select(l => l.Address).FirstOrDefault(),
                Phone = s.Phone,
                Latitude = lat,
                Longitude = lng,
                DistanceKm = distanceKm,
                RatingAverage = s.RatingAverage,
                RatingCount = s.RatingCount,
                OpeningHours = s.OpeningHours,
                GoogleMapsDirectionUrl = lat.HasValue && lng.HasValue
                    ? string.Format(CultureInfo.InvariantCulture, "https://www.google.com/maps/dir/?api=1&destination={0},{1}", lat.Value, lng.Value)
                    : null
            };
        }

        private static StoreDto MapToStoreDto(Store store)
        {
            return new StoreDto
            {
                Id = store.Id,
                OwnerId = store.OwnerId,
                Name = store.Name,
                Description = store.Description,
                Phone = store.Phone,
                Address = store.Address,
                OpeningHours = store.OpeningHours,
                IsActive = store.IsActive,
                RatingAverage = store.RatingAverage,
                RatingCount = store.RatingCount,
                CreatedAt = store.CreatedAt,
                UpdatedAt = store.UpdatedAt,
                Locations = store.Locations.Select(l => new StoreLocationDto
                {
                    Id = l.Id,
                    Latitude = l.Latitude,
                    Longitude = l.Longitude,
                    Address = l.Address
                }).ToList()
            };
        }
    }
}
