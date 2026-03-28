using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using SmartClinic.Constant;
using SmartClinic.Models;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SmartClinic.Services
{
    public class AuthService : IAuthService
    {
        private const int PatientRoleMask = 128;
        private const int OtpExpiryMinutes = 5;
        private const int OtpResendCooldownSeconds = 60;
        private const int MaxOtpAttempts = 5;

        private readonly SmartClinicDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<AuthService> _logger;
        private readonly IEmailService _emailService;

        public AuthService(
            SmartClinicDbContext context,
            IConfiguration configuration,
            IMemoryCache memoryCache,
            ILogger<AuthService> logger,
            IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _memoryCache = memoryCache;
            _logger = logger;
            _emailService = emailService;
        }

        public async Task<AuthResponse?> LoginAsync(string usernameOrEmail, string password)
        {
            var normalizedIdentifier = usernameOrEmail?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedIdentifier) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var normalizedIdentifierLower = normalizedIdentifier.ToLowerInvariant();

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.IsActive == true &&
                (
                    u.Username.Trim().ToLower() == normalizedIdentifierLower ||
                    (u.Email != null && u.Email.Trim().ToLower() == normalizedIdentifierLower)
                ));
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            var (roomId, roomName) = await GetDoctorRoomContextAsync(user);

            // 1. Tạo bộ đôi Token
            var accessToken = GenerateJwtToken(user, roomId, roomName);
            var refreshToken = GenerateRefreshToken();

            // 2. Lưu Refresh Token xuống DB để kiểm soát
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // Sống 7 ngày
            await _context.SaveChangesAsync();

            return new AuthResponse { AccessToken = accessToken, RefreshToken = refreshToken };
        }

        public async Task<PatientRegistrationResult> RegisterPatientAsync(PatientRegistrationRequest request)
        {
            var normalizedUsername = request.Username.Trim();
            var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : request.Email.Trim().ToLowerInvariant();
            var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : request.PhoneNumber.Trim();

            var existingByUsername = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername.ToLower());

            User? existingByEmail = null;
            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                existingByEmail = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);
            }

            if (existingByUsername != null && existingByEmail != null && existingByUsername.Id != existingByEmail.Id)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Tên đăng nhập và email thuộc về 2 tài khoản khác nhau. Vui lòng kiểm tra lại thông tin."
                };
            }

            var targetUser = existingByUsername ?? existingByEmail;

            if (targetUser != null)
            {
                if (targetUser.IsActive != true)
                {
                    return new PatientRegistrationResult
                    {
                        Success = false,
                        Message = "Tài khoản này đang bị khóa. Vui lòng liên hệ quản trị viên."
                    };
                }

                if (!BCrypt.Net.BCrypt.Verify(request.Password, targetUser.PasswordHash))
                {
                    return new PatientRegistrationResult
                    {
                        Success = false,
                        Message = "Mật khẩu không đúng với tài khoản đã tồn tại."
                    };
                }

                if ((targetUser.RoleMask & PatientRoleMask) == PatientRoleMask)
                {
                    return new PatientRegistrationResult
                    {
                        Success = true,
                        Message = "Tài khoản đã có quyền bệnh nhân. Bạn có thể đăng nhập ngay.",
                        AddedPatientRoleToExistingUser = true
                    };
                }

                targetUser.RoleMask |= PatientRoleMask;
                targetUser.FullName = request.FullName.Trim();
                targetUser.Email = normalizedEmail;
                targetUser.PhoneNumber = normalizedPhone;
                targetUser.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
                targetUser.Gender = request.Gender;
                targetUser.DoB = request.DoB;

                await _context.SaveChangesAsync();

                return new PatientRegistrationResult
                {
                    Success = true,
                    Message = "Đã thêm quyền bệnh nhân vào tài khoản hiện có.",
                    AddedPatientRoleToExistingUser = true
                };
            }

            var newUser = new User
            {
                Username = normalizedUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = normalizedPhone,
                Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                Gender = request.Gender,
                DoB = request.DoB,
                RoleMask = PatientRoleMask,
                IsActive = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return new PatientRegistrationResult
            {
                Success = true,
                Message = "Đăng ký tài khoản bệnh nhân thành công.",
                AddedPatientRoleToExistingUser = false
            };
        }

        public async Task<PasswordResetResult> SendPasswordResetOtpAsync(string email)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "Vui lòng nhập email hợp lệ."
                };
            }

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email != null &&
                u.Email.Trim().ToLower() == normalizedEmail &&
                u.IsActive == true);

            if (user == null)
            {
                return new PasswordResetResult
                {
                    Success = true,
                    Message = "Nếu email tồn tại trong hệ thống, OTP đã được gửi."
                };
            }

            var cacheKey = GetOtpCacheKey(normalizedEmail);
            if (_memoryCache.TryGetValue(cacheKey, out PasswordResetOtpSession? existingSession))
            {
                var secondsSinceLastSend = (DateTime.UtcNow - existingSession.LastSentAtUtc).TotalSeconds;
                if (secondsSinceLastSend < OtpResendCooldownSeconds)
                {
                    var waitSeconds = OtpResendCooldownSeconds - (int)secondsSinceLastSend;
                    return new PasswordResetResult
                    {
                        Success = false,
                        Message = $"Bạn vừa yêu cầu OTP. Vui lòng chờ {Math.Max(waitSeconds, 1)} giây để gửi lại."
                    };
                }
            }

            var otp = GenerateOtp();
            var session = new PasswordResetOtpSession
            {
                UserId = user.Id,
                OtpHash = BCrypt.Net.BCrypt.HashPassword(otp),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                LastSentAtUtc = DateTime.UtcNow,
                FailedAttempts = 0
            };

            try
            {
                var subject = "[SmartClinic] Ma OTP dat lai mat khau";
                var receiverName = string.IsNullOrWhiteSpace(user.FullName) ? "ban" : user.FullName;
                var body = $"Xin chao {receiverName},\n\n" +
                           $"Ma OTP dat lai mat khau cua ban la: {otp}\n" +
                           $"Ma co hieu luc trong {OtpExpiryMinutes} phut.\n\n" +
                           "Neu ban khong yeu cau dat lai mat khau, vui long bo qua email nay.\n\n" +
                           "SmartClinic";
                await _emailService.SendEmailAsync(user.Email!, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Send password reset OTP failed for email: {Email}", normalizedEmail);
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "Không thể gửi OTP lúc này. Vui lòng thử lại sau."
                };
            }

            _memoryCache.Set(cacheKey, session, session.ExpiresAtUtc);

            return new PasswordResetResult
            {
                Success = true,
                Message = "OTP đã được gửi đến email của bạn."
            };
        }

        public async Task<PasswordResetResult> ResetPasswordWithOtpAsync(string email, string otp, string newPassword)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "Email không hợp lệ."
                };
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "Vui lòng nhập OTP."
                };
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "Mật khẩu mới phải có ít nhất 6 ký tự."
                };
            }

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email != null &&
                u.Email.Trim().ToLower() == normalizedEmail &&
                u.IsActive == true);

            if (user == null)
            {
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "Không tìm thấy tài khoản hợp lệ với email này."
                };
            }

            var cacheKey = GetOtpCacheKey(normalizedEmail);
            if (!_memoryCache.TryGetValue(cacheKey, out PasswordResetOtpSession? session) || session == null)
            {
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "OTP không tồn tại hoặc đã hết hạn. Vui lòng yêu cầu OTP mới."
                };
            }

            if (session.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _memoryCache.Remove(cacheKey);
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "OTP đã hết hạn. Vui lòng yêu cầu OTP mới."
                };
            }

            if (session.UserId != user.Id)
            {
                _memoryCache.Remove(cacheKey);
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "OTP không hợp lệ. Vui lòng yêu cầu OTP mới."
                };
            }

            var isOtpValid = BCrypt.Net.BCrypt.Verify(otp.Trim(), session.OtpHash);
            if (!isOtpValid)
            {
                session.FailedAttempts++;
                if (session.FailedAttempts >= MaxOtpAttempts)
                {
                    _memoryCache.Remove(cacheKey);
                    return new PasswordResetResult
                    {
                        Success = false,
                        Message = "Bạn đã nhập sai OTP quá nhiều lần. Vui lòng yêu cầu OTP mới."
                    };
                }

                _memoryCache.Set(cacheKey, session, session.ExpiresAtUtc);
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "OTP không đúng. Vui lòng kiểm tra lại."
                };
            }

            Console.WriteLine("OTP verified successfully for user ID: {0}, new password set {1}", user.Id, newPassword);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _context.SaveChangesAsync();

            _memoryCache.Remove(cacheKey);
            return new PasswordResetResult
            {
                Success = true,
                Message = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập lại ngay bây giờ."
            };
        }

        public async Task<AuthResponse?> RenewTokenAsync(string oldRefreshToken)
        {
            // Tìm user có cái Refresh Token này và token chưa hết hạn
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == oldRefreshToken &&
                u.RefreshTokenExpiryTime > DateTime.UtcNow);

            if (user == null) return null; // Token bậy bạ hoặc đã hết hạn -> Bắt đăng nhập lại

            var (roomId, roomName) = await GetDoctorRoomContextAsync(user);

            // Nếu hợp lệ -> Tạo bộ đôi mới (Thu hồi token cũ luôn cho bảo mật xoay vòng - Token Rotation)
            var newAccessToken = GenerateJwtToken(user, roomId, roomName);
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

        private string GenerateJwtToken(User user, int? roomId, string? roomName)
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

            if (roomId.HasValue) claims.Add(new Claim("RoomId", roomId.Value.ToString()));
            if (!string.IsNullOrWhiteSpace(roomName)) claims.Add(new Claim("RoomName", roomName));

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

        private static string GenerateOtp()
        {
            return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        }

        private static string NormalizeEmail(string? email)
        {
            return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
        }

        private static string GetOtpCacheKey(string normalizedEmail)
        {
            return $"password-reset-otp:{normalizedEmail}";
        }



        private async Task<(int? RoomId, string? RoomName)> GetDoctorRoomContextAsync(User user)
        {
            var activeShift = await _context.DoctorShifts
                .Include(s => s.Room)
                .FirstOrDefaultAsync(s => s.DoctorId == user.Id && s.StatusEnum == DoctorShiftStatus.Active);

            if (activeShift is not null)
            {
                return (activeShift.RoomId, activeShift.Room.Name);
            }

            return (null, null);
        }

        private sealed class PasswordResetOtpSession
        {
            public int UserId { get; set; }
            public string OtpHash { get; set; } = string.Empty;
            public DateTime ExpiresAtUtc { get; set; }
            public DateTime LastSentAtUtc { get; set; }
            public int FailedAttempts { get; set; }
        }

    }
}