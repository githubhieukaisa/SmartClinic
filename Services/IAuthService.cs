using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface IAuthService
    {
        Task<AuthResponse?> LoginAsync(string username, string password);
        Task<AuthResponse?> RenewTokenAsync(string oldRefreshToken);
        Task LogoutAsync(int userId);
    }
}
