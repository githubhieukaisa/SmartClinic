using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartClinic.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SmartClinic.Services
{
    public class AuthService : IAuthService
    {
        private readonly SmartClinicDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(SmartClinicDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponse?> LoginAsync(string username, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive == true);
            Console.WriteLine($"[AuthService] Login attempt for username: {username}, password: {password}");
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            // 1. Tạo bộ đôi Token
            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            // 2. Lưu Refresh Token xuống DB để kiểm soát
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // Sống 7 ngày
            await _context.SaveChangesAsync();

            return new AuthResponse { AccessToken = accessToken, RefreshToken = refreshToken };
        }

        public async Task<AuthResponse?> RenewTokenAsync(string oldRefreshToken)
        {
            // Tìm user có cái Refresh Token này và token chưa hết hạn
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == oldRefreshToken &&
                u.RefreshTokenExpiryTime > DateTime.UtcNow);

            if (user == null) return null; // Token bậy bạ hoặc đã hết hạn -> Bắt đăng nhập lại

            // Nếu hợp lệ -> Tạo bộ đôi mới (Thu hồi token cũ luôn cho bảo mật xoay vòng - Token Rotation)
            var newAccessToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return new AuthResponse { AccessToken = newAccessToken, RefreshToken = newRefreshToken };
        }

        public async Task LogoutAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.RefreshToken = null; // Xóa token -> Hết đường làm mới
                await _context.SaveChangesAsync();
            }
        }

        private string GenerateJwtToken(User user)
        {
            var secretKey = _configuration["Jwt:Key"];
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("FullName", user.FullName ?? ""),
                new Claim("RoleMask", user.RoleMask.ToString())
            };

            if (user.RoomId.HasValue) claims.Add(new Claim("RoomId", user.RoomId.Value.ToString()));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}