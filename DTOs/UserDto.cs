using System.ComponentModel.DataAnnotations;

namespace SmartClinic.DTOs
{
    /// <summary>
    /// DTO dùng khi Admin TẠO MỚI một tài khoản user.
    /// Có trường Password vì lần đầu phải đặt mật khẩu.
    /// </summary>
    public class CreateUserDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập!")]
        [MaxLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự!")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu!")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự!")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập họ tên!")]
        [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự!")]
        public string FullName { get; set; } = "";

        [EmailAddress(ErrorMessage = "Email không hợp lệ!")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ!")]
        public string? PhoneNumber { get; set; }

        public string? Address { get; set; }
        public bool? Gender { get; set; }

        /// <summary>
        /// Giá trị tổng hợp từ các checkbox quyền (Bitwise OR).
        /// Ví dụ: Bác sĩ + Admin = 2 | 16 = 18
        /// </summary>
        public int RoleMask { get; set; } = 0;

        /// <summary>
        /// Phòng khám được gán (chỉ dùng cho Bác sĩ).
        /// </summary>
        public int? RoomId { get; set; }
    }

    /// <summary>
    /// DTO dùng khi Admin CHỈNH SỬA thông tin user.
    /// KHÔNG có trường Password — reset mật khẩu là thao tác riêng.
    /// </summary>
    public class EditUserDto
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên!")]
        [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự!")]
        public string FullName { get; set; } = "";

        [EmailAddress(ErrorMessage = "Email không hợp lệ!")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ!")]
        public string? PhoneNumber { get; set; }

        public string? Address { get; set; }
        public bool? Gender { get; set; }
        public int RoleMask { get; set; } = 0;
        public int? RoomId { get; set; }
    }
}
