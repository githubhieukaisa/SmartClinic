using System.ComponentModel.DataAnnotations;

namespace SmartClinic.DTOs
{
    public class ProfileUpdateDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập!")]
        [MaxLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự!")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ và tên!")]
        [MaxLength(100, ErrorMessage = "Họ và tên tối đa 100 ký tự!")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ!")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ!")]
        public string? PhoneNumber { get; set; }

        public string? Address { get; set; }
        public bool? Gender { get; set; }

        [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự!")]
        public string? NewPassword { get; set; }

        [MinLength(6, ErrorMessage = "Mật khẩu hiện tại phải có ít nhất 6 ký tự!")]
        public string? CurrentPassword { get; set; }
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp!")]
        public string? ConfirmNewPassword { get; set; }
    }

    public class ProfileUpdateResult
    {
        public bool Success { get; set; }
        public bool HasChanges { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
