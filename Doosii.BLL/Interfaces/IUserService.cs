using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IUserService
    {
        Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<MerchantProfileDto> SubmitSellerApplicationAsync(int userId, SellerApplicationRequest request);
        Task<MerchantProfileDto?> GetMySellerApplicationAsync(int userId);
        Task<MerchantProfileDto> UpdateMySellerApplicationAsync(int userId, UpdateSellerApplicationRequest request);
        Task DeleteMySellerApplicationAsync(int userId);
    }
}