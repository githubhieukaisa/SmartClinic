using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface IAuthService
    {
        Task<AuthResponse?> LoginAsync(string usernameOrEmail, string password);
        Task<PatientRegistrationResult> RegisterPatientAsync(PatientRegistrationRequest request);
        Task<PatientRegistrationResult> ConfirmPatientRegistrationAsync(string email, string otp);
        Task<PasswordResetResult> ResendPatientRegistrationOtpAsync(string email);
        Task<PasswordResetResult> SendPasswordResetOtpAsync(string email);
        Task<PasswordResetResult> ResetPasswordWithOtpAsync(string email, string otp, string newPassword);
        Task<AuthResponse?> RenewTokenAsync(string oldRefreshToken);
        Task LogoutAsync(int userId);

        // Medical History Access OTP (Distributed via MemoryCache + Email)
        Task<PasswordResetResult> SendHistoryAccessOtpAsync(int ticketId, string email, string patientName);
        Task<bool> VerifyHistoryAccessOtpAsync(int ticketId, string otp);
    }
}
