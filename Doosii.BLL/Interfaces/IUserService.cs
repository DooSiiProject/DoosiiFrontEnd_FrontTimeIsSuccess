using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IUserService
    {
        Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<MerchantProfileDto> SubmitSellerApplicationAsync(int userId, SellerApplicationRequest request);
    }
}