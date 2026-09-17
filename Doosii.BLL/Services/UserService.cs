using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;
using Doosii.DAL.Models;

namespace Doosii.BLL.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Khong tim thay nguoi dung.");
            }

            user.FullName = request.FullName.Trim();
            user.AvatarUrl = request.AvatarUrl?.Trim();
            user.DeliveryAddress = request.DeliveryAddress?.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToUserDto(user);
        }

        public async Task<MerchantProfileDto> SubmitSellerApplicationAsync(int userId, SellerApplicationRequest request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Khong tim thay nguoi dung.");
            }

            if (user.Role != "Customer")
            {
                throw new InvalidOperationException("Chi nguoi dung co vai tro Customer moi duoc nop ho so Seller.");
            }

            var existingPending = await _context.MerchantProfiles
                .FirstOrDefaultAsync(m => m.UserId == userId && m.KycStatus == "PENDING");

            if (existingPending != null)
            {
                throw new InvalidOperationException("Ban da co ho so KYC dang cho duyet (PENDING). Vui long cho ket qua truoc khi gui lai.");
            }

            var profile = new MerchantProfile
            {
                UserId = userId,
                StoreName = request.StoreName.Trim(),
                Phone = request.Phone.Trim(),
                Address = request.Address.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                KycStatus = "PENDING",
                LicenseImageUrl = request.LicenseImageUrl.Trim(),
                FrontFacadeUrl = request.FrontFacadeUrl.Trim(),
                IdCardFrontUrl = request.IdCardFrontUrl.Trim(),
                IdCardBackUrl = request.IdCardBackUrl.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.MerchantProfiles.Add(profile);
            await _context.SaveChangesAsync();

            return MapToMerchantProfileDto(profile);
        }

        private static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
        }

        private static MerchantProfileDto MapToMerchantProfileDto(MerchantProfile m)
        {
            return new MerchantProfileDto
            {
                Id = m.Id,
                UserId = m.UserId,
                StoreName = m.StoreName,
                Phone = m.Phone,
                Address = m.Address,
                Latitude = m.Latitude,
                Longitude = m.Longitude,
                KycStatus = m.KycStatus,
                LicenseImageUrl = m.LicenseImageUrl,
                FrontFacadeUrl = m.FrontFacadeUrl,
                IdCardFrontUrl = m.IdCardFrontUrl,
                IdCardBackUrl = m.IdCardBackUrl,
                RejectionReason = m.RejectionReason,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            };
        }
    }
}