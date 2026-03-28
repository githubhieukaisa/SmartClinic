namespace SmartClinic.Models
{
    public class PatientRegistrationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public bool AddedPatientRoleToExistingUser { get; init; }

        /// <summary>
        /// True khi đã gửi OTP đến email; cần gọi xác nhận OTP để hoàn tất (chỉ tài khoản bệnh nhân mới).
        /// </summary>
        public bool AwaitingEmailOtp { get; init; }
    }
}