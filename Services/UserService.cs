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
                .Include(u => u.Room)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Room)
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
                RoomId = dto.RoomId,
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
            user.RoomId = dto.RoomId;

            await _context.SaveChangesAsync();
            return true;
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
    }
}
