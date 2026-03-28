using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using SmartClinic.DTOs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class UserService : IUserService
    {
        private readonly SmartClinicDbContext _context;

        public UserService(SmartClinicDbContext context)
        {
            _context = context;
        }

        // ──────────── READ ────────────

        public async Task<List<User>> GetAllUsersAsync()
        {
            // Lấy tất cả user, sắp xếp theo ngày tạo mới nhất lên đầu.
            // Include Room để hiển thị tên phòng nếu user là Bác sĩ.
            return await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        // ──────────── CREATE ────────────

        public async Task<bool> CreateUserAsync(CreateUserDto dto)
        {
            // Kiểm tra trùng username trước khi tạo
            if (await IsUsernameExistsAsync(dto.Username))
                return false;

            var user = new User
            {
                Username = dto.Username,
                // Hash password bằng BCrypt — KHÔNG BAO GIỜ lưu plaintext!
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                Gender = dto.Gender,
                RoleMask = dto.RoleMask,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return true;
        }

        // ──────────── UPDATE ────────────

        public async Task<bool> UpdateUserAsync(int id, EditUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            // Chỉ cập nhật các trường thông tin, KHÔNG đụng vào Password
            user.FullName = dto.FullName;
            user.Email = dto.Email;
            user.PhoneNumber = dto.PhoneNumber;
            user.Address = dto.Address;
            user.Gender = dto.Gender;
            user.RoleMask = dto.RoleMask;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ProfileUpdateResult> UpdateProfileAsync(int id, ProfileUpdateDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return new ProfileUpdateResult
                {
                    Success = false,
                    Message = "Không tìm thấy tài khoản."
                };
            }

            if (!EqualsIgnoreCase(user.Username, dto.Username))
            {
                return new ProfileUpdateResult
                {
                    Success = false,
                    Message = "Tên đăng nhập không thể thay đổi."
                };
            }

            var payload = BuildProfilePayload(dto);
            var changes = DetectProfileChanges(user, payload);

            var uniqueError = await ValidateUniqueConstraintsAsync(id, payload, changes);
            if (uniqueError != null)
            {
                return new ProfileUpdateResult
                {
                    Success = false,
                    Message = uniqueError
                };
            }

            var passwordResult = ApplyPasswordChangeIfRequested(user, dto);
            if (!passwordResult.Success)
            {
                return new ProfileUpdateResult
                {
                    Success = false,
                    Message = passwordResult.Message
                };
            }

            changes.PasswordChanged = passwordResult.HasChanges;
            if (!changes.HasAnyChanges)
            {
                return new ProfileUpdateResult
                {
                    Success = true,
                    HasChanges = false,
                    Message = "Không có thay đổi để cập nhật."
                };
            }

            ApplyProfileChanges(user, payload, changes);

            await _context.SaveChangesAsync();

            return new ProfileUpdateResult
            {
                Success = true,
                HasChanges = true,
                Message = "Cập nhật hồ sơ thành công."
            };
        }

        // ──────────── TOGGLE ACTIVE ────────────

        public async Task<bool> ToggleActiveAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            // Đảo ngược trạng thái: true -> false, false -> true
            user.IsActive = !(user.IsActive ?? true);
            await _context.SaveChangesAsync();
            return true;
        }

        // ──────────── RESET PASSWORD ────────────

        public async Task<bool> ResetPasswordAsync(int id, string newPassword)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            return true;
        }

        // ──────────── HELPER ────────────

        public async Task<bool> IsUsernameExistsAsync(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username);
        }

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static bool EqualsTrimmed(string? left, string? right)
        {
            return string.Equals(NormalizeOptional(left), NormalizeOptional(right), StringComparison.Ordinal);
        }

        private static bool EqualsIgnoreCase(string? left, string? right)
        {
            return string.Equals(NormalizeOptional(left), NormalizeOptional(right), StringComparison.OrdinalIgnoreCase);
        }

        private static ProfilePayload BuildProfilePayload(ProfileUpdateDto dto)
        {
            return new ProfilePayload
            {
                Username = dto.Username.Trim(),
                FullName = dto.FullName.Trim(),
                Email = NormalizeOptional(dto.Email),
                PhoneNumber = NormalizeOptional(dto.PhoneNumber),
                Address = NormalizeOptional(dto.Address),
                Gender = dto.Gender
            };
        }

        private static ProfileChanges DetectProfileChanges(User user, ProfilePayload payload)
        {
            return new ProfileChanges
            {
                FullNameChanged = !EqualsTrimmed(user.FullName, payload.FullName),
                EmailChanged = !EqualsIgnoreCase(user.Email, payload.Email),
                PhoneChanged = !EqualsTrimmed(user.PhoneNumber, payload.PhoneNumber),
                AddressChanged = !EqualsTrimmed(user.Address, payload.Address),
                GenderChanged = user.Gender != payload.Gender
            };
        }

        private async Task<string?> ValidateUniqueConstraintsAsync(int userId, ProfilePayload payload, ProfileChanges changes)
        {
            if (changes.EmailChanged && !string.IsNullOrWhiteSpace(payload.Email))
            {
                var normalizedEmail = payload.Email.ToLower();
                var emailExists = await _context.Users.AnyAsync(u =>
                    u.Id != userId &&
                    u.Email != null &&
                    u.Email.Trim().ToLower() == normalizedEmail);
                if (emailExists)
                {
                    return "Email đã tồn tại.";
                }
            }

            if (changes.PhoneChanged && !string.IsNullOrWhiteSpace(payload.PhoneNumber))
            {
                var phoneExists = await _context.Users.AnyAsync(u =>
                    u.Id != userId &&
                    u.PhoneNumber != null &&
                    u.PhoneNumber.Trim() == payload.PhoneNumber);
                if (phoneExists)
                {
                    return "Số điện thoại đã tồn tại.";
                }
            }

            return null;
        }

        private static PasswordChangeResult ApplyPasswordChangeIfRequested(User user, ProfileUpdateDto dto)
        {
            var wantsPasswordChange =
                !string.IsNullOrWhiteSpace(dto.CurrentPassword) ||
                !string.IsNullOrWhiteSpace(dto.NewPassword) ||
                !string.IsNullOrWhiteSpace(dto.ConfirmNewPassword);

            if (!wantsPasswordChange)
            {
                return PasswordChangeResult.NoChange();
            }

            if (string.IsNullOrWhiteSpace(dto.CurrentPassword) ||
                string.IsNullOrWhiteSpace(dto.NewPassword) ||
                string.IsNullOrWhiteSpace(dto.ConfirmNewPassword))
            {
                return PasswordChangeResult.Fail("Vui lòng nhập đủ mật khẩu hiện tại, mật khẩu mới và xác nhận mật khẩu mới.");
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                return PasswordChangeResult.Fail("Mật khẩu hiện tại không đúng.");
            }

            if (!string.Equals(dto.NewPassword, dto.ConfirmNewPassword, StringComparison.Ordinal))
            {
                return PasswordChangeResult.Fail("Xác nhận mật khẩu mới không khớp.");
            }

            if (BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash))
            {
                return PasswordChangeResult.Fail("Mật khẩu mới phải khác mật khẩu hiện tại.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            return PasswordChangeResult.Changed();
        }

        private static void ApplyProfileChanges(User user, ProfilePayload payload, ProfileChanges changes)
        {
            if (changes.FullNameChanged) user.FullName = payload.FullName;
            if (changes.EmailChanged) user.Email = payload.Email;
            if (changes.PhoneChanged) user.PhoneNumber = payload.PhoneNumber;
            if (changes.AddressChanged) user.Address = payload.Address;
            if (changes.GenderChanged) user.Gender = payload.Gender;
        }

        private sealed class ProfilePayload
        {
            public string Username { get; init; } = string.Empty;
            public string FullName { get; init; } = string.Empty;
            public string? Email { get; init; }
            public string? PhoneNumber { get; init; }
            public string? Address { get; init; }
            public bool? Gender { get; init; }
        }

        private sealed class ProfileChanges
        {
            public bool FullNameChanged { get; init; }
            public bool EmailChanged { get; init; }
            public bool PhoneChanged { get; init; }
            public bool AddressChanged { get; init; }
            public bool GenderChanged { get; init; }
            public bool PasswordChanged { get; set; }

            public bool HasAnyChanges =>
                FullNameChanged ||
                EmailChanged ||
                PhoneChanged ||
                AddressChanged ||
                GenderChanged ||
                PasswordChanged;
        }

        private sealed class PasswordChangeResult
        {
            public bool Success { get; init; }
            public bool HasChanges { get; init; }
            public string Message { get; init; } = string.Empty;

            public static PasswordChangeResult NoChange() => new() { Success = true, HasChanges = false };

            public static PasswordChangeResult Changed() => new() { Success = true, HasChanges = true };

            public static PasswordChangeResult Fail(string message) => new() { Success = false, Message = message };
        }
    }
}
