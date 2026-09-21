using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces
{
    public interface IAuthService
    {
        Task<UserDto> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshTokenAsync(string refreshToken);
        Task<UserDto> GetCurrentUserAsync(int userId);
    }
}
