using System.ComponentModel.DataAnnotations;

namespace SmartClinic.DTOs
{
    public class ResetPasswordDto
    {
        [Required(ErrorMessage = "Vui lòng nhập email!")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ!")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập OTP!")]
        [RegularExpression("^\\d{6}$", ErrorMessage = "OTP phải gồm đúng 6 chữ số!")]
        public string Otp { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới!")]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự!")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới!")]
        [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp!")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
