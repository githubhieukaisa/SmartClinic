using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface IAuthService
    {
        Task<AuthResponse?> LoginAsync(string usernameOrEmail, string password);
        Task<PatientRegistrationResult> RegisterPatientAsync(PatientRegistrationRequest request);
        Task<PasswordResetResult> SendPasswordResetOtpAsync(string email);
        Task<PasswordResetResult> ResetPasswordWithOtpAsync(string email, string otp, string newPassword);
        Task<AuthResponse?> RenewTokenAsync(string oldRefreshToken);
        Task LogoutAsync(int userId);
    }
}
