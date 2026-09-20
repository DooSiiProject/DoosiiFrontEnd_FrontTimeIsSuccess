using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
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

            // Update or create location
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