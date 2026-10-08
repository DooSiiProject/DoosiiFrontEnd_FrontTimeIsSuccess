using System.Text.Json;
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

            var existingProfile = await _context.MerchantProfiles
                .FirstOrDefaultAsync(m => m.UserId == userId);

            if (existingProfile != null && existingProfile.KycStatus == "PENDING")
            {
                throw new InvalidOperationException("Ban da co ho so KYC dang cho duyet (PENDING). Vui long cho ket qua truoc khi gui lai.");
            }

            if (existingProfile != null && existingProfile.KycStatus == "APPROVED")
            {
                throw new InvalidOperationException("Ho so KYC cua ban da duoc duyet. Tai khoan da la Seller.");
            }

            // Xử lý địa chỉ theo kiểu địa chỉ (OLD hoặc NEW)
            var addressType = string.IsNullOrWhiteSpace(request.AddressType) ? "NEW" : request.AddressType.Trim().ToUpper();
            string finalAddress;
            if (addressType == "OLD")
            {
                finalAddress = !string.IsNullOrWhiteSpace(request.Address)
                    ? request.Address.Trim()
                    : (!string.IsNullOrWhiteSpace(user.DeliveryAddress) ? user.DeliveryAddress.Trim() : string.Empty);

                if (string.IsNullOrWhiteSpace(finalAddress))
                {
                    throw new InvalidOperationException("Ban da chon su dung dia chi cu nhung tai khoan chua co dia chi mac dinh. Vui long nhap dia chi.");
                }
            }
            else
            {
                finalAddress = request.Address?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(finalAddress))
                {
                    throw new InvalidOperationException("Dia chi cua hang la bat buoc.");
                }
            }

            var contactName = !string.IsNullOrWhiteSpace(request.ContactName) ? request.ContactName.Trim() : user.FullName;
            var contactEmail = !string.IsNullOrWhiteSpace(request.ContactEmail) ? request.ContactEmail.Trim() : user.Email;
            var shopMediaJson = request.ShopMediaUrls != null && request.ShopMediaUrls.Count > 0
                ? JsonSerializer.Serialize(request.ShopMediaUrls)
                : null;

            var facadeUrl = !string.IsNullOrWhiteSpace(request.FrontFacadeUrl)
                ? request.FrontFacadeUrl.Trim()
                : (request.ShopMediaUrls != null && request.ShopMediaUrls.Count > 0 ? request.ShopMediaUrls[0] : string.Empty);

            var licenseUrl = request.LicenseImageUrl?.Trim() ?? string.Empty;

            if (existingProfile != null)
            {
                // Nếu hồ sơ trước đó bị REJECTED, cho phép nộp lại bằng cách cập nhật bản ghi cũ
                existingProfile.StoreName = request.StoreName.Trim();
                existingProfile.ContactName = contactName;
                existingProfile.ContactEmail = contactEmail;
                existingProfile.Phone = request.Phone.Trim();
                existingProfile.Address = finalAddress;
                existingProfile.AddressType = addressType;
                existingProfile.Latitude = request.Latitude;
                existingProfile.Longitude = request.Longitude;
                existingProfile.EstablishedDate = request.EstablishedDate;
                existingProfile.TaxCode = request.TaxCode?.Trim();
                existingProfile.ShopMediaUrls = shopMediaJson;
                existingProfile.LicenseImageUrl = licenseUrl;
                existingProfile.FrontFacadeUrl = facadeUrl;
                existingProfile.IdCardFrontUrl = request.IdCardFrontUrl.Trim();
                existingProfile.IdCardBackUrl = request.IdCardBackUrl.Trim();
                existingProfile.KycStatus = "PENDING";
                existingProfile.RejectionReason = null;
                existingProfile.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return MapToMerchantProfileDto(existingProfile);
            }

            var profile = new MerchantProfile
            {
                UserId = userId,
                StoreName = request.StoreName.Trim(),
                ContactName = contactName,
                ContactEmail = contactEmail,
                Phone = request.Phone.Trim(),
                Address = finalAddress,
                AddressType = addressType,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                EstablishedDate = request.EstablishedDate,
                TaxCode = request.TaxCode?.Trim(),
                ShopMediaUrls = shopMediaJson,
                LicenseImageUrl = licenseUrl,
                FrontFacadeUrl = facadeUrl,
                IdCardFrontUrl = request.IdCardFrontUrl.Trim(),
                IdCardBackUrl = request.IdCardBackUrl.Trim(),
                KycStatus = "PENDING",
                CreatedAt = DateTime.UtcNow
            };

            _context.MerchantProfiles.Add(profile);
            await _context.SaveChangesAsync();

            return MapToMerchantProfileDto(profile);
        }

        public async Task<MerchantProfileDto?> GetMySellerApplicationAsync(int userId)
        {
            var profile = await _context.MerchantProfiles
                .FirstOrDefaultAsync(m => m.UserId == userId);

            return profile != null ? MapToMerchantProfileDto(profile) : null;
        }

        public async Task<MerchantProfileDto> UpdateMySellerApplicationAsync(int userId, UpdateSellerApplicationRequest request)
        {
            var profile = await _context.MerchantProfiles
                .FirstOrDefaultAsync(m => m.UserId == userId);

            if (profile == null)
            {
                throw new KeyNotFoundException("Ban chua co ho so dang ky mo shop nao.");
            }

            if (profile.KycStatus == "APPROVED")
            {
                throw new InvalidOperationException("Ho so cua ban da duoc duyet. Khong the chinh sua.");
            }

            var user = await _context.Users.FindAsync(userId);

            // Xử lý địa chỉ theo kiểu địa chỉ
            var addressType = string.IsNullOrWhiteSpace(request.AddressType) ? (profile.AddressType ?? "NEW") : request.AddressType.Trim().ToUpper();
            string finalAddress;
            if (addressType == "OLD")
            {
                finalAddress = !string.IsNullOrWhiteSpace(request.Address)
                    ? request.Address.Trim()
                    : (!string.IsNullOrWhiteSpace(user?.DeliveryAddress) ? user.DeliveryAddress.Trim() : profile.Address);
            }
            else
            {
                finalAddress = !string.IsNullOrWhiteSpace(request.Address) ? request.Address.Trim() : profile.Address;
            }

            if (!string.IsNullOrWhiteSpace(request.StoreName)) profile.StoreName = request.StoreName.Trim();
            if (request.ContactName != null) profile.ContactName = request.ContactName.Trim();
            if (request.ContactEmail != null) profile.ContactEmail = request.ContactEmail.Trim();
            if (!string.IsNullOrWhiteSpace(request.Phone)) profile.Phone = request.Phone.Trim();
            profile.Address = finalAddress;
            profile.AddressType = addressType;
            if (request.Latitude.HasValue) profile.Latitude = request.Latitude;
            if (request.Longitude.HasValue) profile.Longitude = request.Longitude;
            if (request.EstablishedDate.HasValue) profile.EstablishedDate = request.EstablishedDate;
            if (request.TaxCode != null) profile.TaxCode = request.TaxCode.Trim();

            if (request.ShopMediaUrls != null)
            {
                profile.ShopMediaUrls = JsonSerializer.Serialize(request.ShopMediaUrls);
                if (string.IsNullOrWhiteSpace(request.FrontFacadeUrl) && request.ShopMediaUrls.Count > 0)
                {
                    profile.FrontFacadeUrl = request.ShopMediaUrls[0];
                }
            }

            if (request.LicenseImageUrl != null) profile.LicenseImageUrl = request.LicenseImageUrl.Trim();
            if (request.FrontFacadeUrl != null) profile.FrontFacadeUrl = request.FrontFacadeUrl.Trim();
            if (!string.IsNullOrWhiteSpace(request.IdCardFrontUrl)) profile.IdCardFrontUrl = request.IdCardFrontUrl.Trim();
            if (!string.IsNullOrWhiteSpace(request.IdCardBackUrl)) profile.IdCardBackUrl = request.IdCardBackUrl.Trim();

            // Nếu đơn trước đó bị từ chối, cập nhật lại sẽ chuyển về PENDING để Admin xem xét lại
            if (profile.KycStatus == "REJECTED")
            {
                profile.KycStatus = "PENDING";
                profile.RejectionReason = null;
            }

            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapToMerchantProfileDto(profile);
        }

        public async Task DeleteMySellerApplicationAsync(int userId)
        {
            var profile = await _context.MerchantProfiles
                .FirstOrDefaultAsync(m => m.UserId == userId);

            if (profile == null)
            {
                throw new KeyNotFoundException("Khong tim thay ho so dang ky mo shop cua ban.");
            }

            if (profile.KycStatus == "APPROVED")
            {
                throw new InvalidOperationException("Ho so da duoc duyet. Khong the xoa ho so nay.");
            }

            _context.MerchantProfiles.Remove(profile);
            await _context.SaveChangesAsync();
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
            List<string> mediaUrls = new List<string>();
            if (!string.IsNullOrWhiteSpace(m.ShopMediaUrls))
            {
                try
                {
                    mediaUrls = JsonSerializer.Deserialize<List<string>>(m.ShopMediaUrls) ?? new List<string>();
                }
                catch
                {
                    mediaUrls = new List<string>();
                }
            }
            else if (!string.IsNullOrWhiteSpace(m.FrontFacadeUrl))
            {
                mediaUrls.Add(m.FrontFacadeUrl);
            }

            return new MerchantProfileDto
            {
                Id = m.Id,
                UserId = m.UserId,
                ContactName = m.ContactName ?? m.User?.FullName,
                ContactEmail = m.ContactEmail ?? m.User?.Email,
                StoreName = m.StoreName,
                Phone = m.Phone,
                Address = m.Address,
                AddressType = m.AddressType ?? "NEW",
                Latitude = m.Latitude,
                Longitude = m.Longitude,
                EstablishedDate = m.EstablishedDate,
                TaxCode = m.TaxCode,
                ShopMediaUrls = mediaUrls,
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