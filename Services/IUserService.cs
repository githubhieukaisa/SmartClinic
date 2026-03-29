using SmartClinic.DTOs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface IUserService
    {
        /// <summary>Lấy toàn bộ danh sách user (kể cả đã bị vô hiệu hóa).</summary>
        Task<List<User>> GetAllUsersAsync();

        /// <summary>Lấy danh sách chuyên khoa để gán cho bác sĩ/nhân viên xét nghiệm.</summary>
        Task<List<Department>> GetDepartmentsAsync();

        /// <summary>Lấy 1 user theo Id.</summary>
        Task<User?> GetUserByIdAsync(int id);

        /// <summary>Tạo user mới, hash password bằng BCrypt.</summary>
        Task<bool> CreateUserAsync(CreateUserDto dto);

        /// <summary>Cập nhật thông tin user (không đổi password).</summary>
        Task<bool> UpdateUserAsync(int id, EditUserDto dto);

        /// <summary>Cập nhật hồ sơ người dùng hiện tại (có thể đổi mật khẩu).</summary>
        Task<ProfileUpdateResult> UpdateProfileAsync(int id, ProfileUpdateDto dto);

        /// <summary>Bật/tắt trạng thái IsActive (soft disable).</summary>
        Task<bool> ToggleActiveAsync(int id);

        /// <summary>Reset mật khẩu cho user.</summary>
        Task<bool> ResetPasswordAsync(int id, string newPassword);

        /// <summary>Kiểm tra username đã tồn tại chưa.</summary>
        Task<bool> IsUsernameExistsAsync(string username);
    }
}
