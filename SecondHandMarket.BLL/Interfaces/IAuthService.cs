using SecondHandMarket.BLL.DTOs;

namespace SecondHandMarket.BLL.Interfaces
{
    public interface IAuthService
    {
        Task<UserDto> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<UserDto> GetCurrentUserAsync(int userId);
    }
}
