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
        // Khớp với AddCustomAuthorization (ServiceExtensions): bit vai trò nội bộ (không tính Patient).
        private const int ReceptionRoleMask = 1;
        private const int DoctorRoleMask = 2;
        private const int PharmacistRoleMask = 4;
        private const int CashierRoleMask = 8;
        private const int AdminRoleMask = 16;
        private const int LabTechRoleMask = 32;
        private const int ManagerRoleMask = 64;
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
            var normalizedPhoneDigits = NormalizeVietnamPhoneDigits(request.PhoneNumber);
            var normalizedPhone = string.IsNullOrEmpty(normalizedPhoneDigits)
                ? null
                : normalizedPhoneDigits;

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (request.DoB.HasValue && request.DoB.Value > today)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Ngày sinh không được sau ngày hiện tại."
                };
            }

            var existingByUsername = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername.ToLower());

            User? existingByEmail = null;
            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                existingByEmail = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);
            }

            User? existingByPhone = null;
            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                existingByPhone = await FindUserByNormalizedPhoneAsync(normalizedPhone);
            }

            var matchedIds = new HashSet<int>();
            if (existingByUsername != null) matchedIds.Add(existingByUsername.Id);
            if (existingByEmail != null) matchedIds.Add(existingByEmail.Id);
            if (existingByPhone != null) matchedIds.Add(existingByPhone.Id);

            if (matchedIds.Count > 1)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Thông tin tên đăng nhập, email hoặc số điện thoại đang thuộc về các tài khoản khác nhau. Vui lòng kiểm tra lại thông tin."
                };
            }

            var targetUser = existingByUsername ?? existingByEmail ?? existingByPhone;

            if (targetUser != null)
            {
                var isPhoneClaimFlow = existingByPhone != null
                    && existingByPhone.Id == targetUser.Id
                    && existingByUsername == null
                    && existingByEmail == null;

                if (isPhoneClaimFlow)
                {
                    if (!CanClaimPreRegisteredPatient(targetUser))
                    {
                        return new PatientRegistrationResult
                        {
                            Success = false,
                            Message = "Số điện thoại này đã gắn với tài khoản khác. Vui lòng đăng nhập tài khoản hiện có hoặc liên hệ lễ tân để được hỗ trợ."
                        };
                    }

                    if (string.IsNullOrWhiteSpace(normalizedEmail))
                    {
                        return new PatientRegistrationResult
                        {
                            Success = false,
                            Message = "Vui lòng nhập email để nhận OTP kích hoạt tài khoản."
                        };
                    }

                    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                    {
                        return new PatientRegistrationResult
                        {
                            Success = false,
                            Message = "Mật khẩu phải có ít nhất 6 ký tự."
                        };
                    }

                    return await SendPatientRegistrationOtpAsyncInternal(
                        request,
                        normalizedUsername,
                        normalizedEmail,
                        normalizedPhone!,
                        targetUser.Id);
                }

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
                        Message = "Tài khoản này đã có quyền bệnh nhân. Vui lòng đăng nhập bằng tên đăng nhập và mật khẩu hiện tại.",
                        AddedPatientRoleToExistingUser = true
                    };
                }

                // Chỉ khi chưa là bệnh nhân: gắn thêm Patient. Nếu đã có vai trò nội bộ khác, giữ nguyên đăng nhập/mật khẩu/email.
                var nonPatientRolesBefore = targetUser.RoleMask & ~PatientRoleMask;
                targetUser.RoleMask |= PatientRoleMask;

                await _context.SaveChangesAsync();

                var message = nonPatientRolesBefore != 0
                    ? BuildStaffPatientActivationMessage(nonPatientRolesBefore)
                    : "Đã bổ sung quyền bệnh nhân cho tài khoản hiện có. Bạn có thể đăng nhập ngay.";

                return new PatientRegistrationResult
                {
                    Success = true,
                    Message = message,
                    AddedPatientRoleToExistingUser = true
                };
            }

            // Tài khoản hoàn toàn mới — kiểm tra đầy đủ & trùng lặp thực tế cho bệnh nhân tự đăng ký.
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Vui lòng nhập email để tạo tài khoản bệnh nhân."
                };
            }

            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Vui lòng nhập số điện thoại."
                };
            }

            if (!request.DoB.HasValue)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Vui lòng nhập ngày sinh."
                };
            }

            if (request.Gender == null)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Vui lòng chọn giới tính."
                };
            }

            if (string.IsNullOrWhiteSpace(request.Address))
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Vui lòng nhập địa chỉ."
                };
            }

            var emailTaken = await _context.Users.AnyAsync(u =>
                u.Email != null && u.Email.Trim().ToLower() == normalizedEmail);
            if (emailTaken)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Email này đã được sử dụng. Nếu đây là tài khoản của bạn, hãy đăng nhập hoặc dùng đúng tên đăng nhập/email kèm mật khẩu để kích hoạt quyền bệnh nhân."
                };
            }

            var phoneOwners = await _context.Users.AsNoTracking()
                .Where(u => u.PhoneNumber != null && u.PhoneNumber != "")
                .Select(u => new { u.Id, u.PhoneNumber })
                .ToListAsync();
            var phoneDup = phoneOwners.FirstOrDefault(u =>
                !string.IsNullOrEmpty(normalizedPhone) &&
                NormalizeVietnamPhoneDigits(u.PhoneNumber) == normalizedPhone);
            if (phoneDup != null)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Số điện thoại đã gắn với tài khoản khác. Vui lòng dùng số khác hoặc liên hệ lễ tân nếu thông tin của bạn đã có trong hệ thống."
                };
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Mật khẩu phải có ít nhất 6 ký tự."
                };
            }

            return await SendPatientRegistrationOtpAsyncInternal(
                request,
                normalizedUsername,
                normalizedEmail,
                normalizedPhone,
                null);
        }

        public async Task<PatientRegistrationResult> ConfirmPatientRegistrationAsync(string email, string otp)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Email không hợp lệ."
                };
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Vui lòng nhập mã OTP."
                };
            }

            var cacheKey = GetPatientRegistrationOtpCacheKey(normalizedEmail);
            if (!_memoryCache.TryGetValue(cacheKey, out PendingPatientRegistrationSession? session) || session == null)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Mã xác nhận không tồn tại hoặc đã hết hạn. Vui lòng gửi lại mã từ bước đăng ký."
                };
            }

            if (!string.Equals(session.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                _memoryCache.Remove(cacheKey);
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Phiên đăng ký không khớp email. Vui lòng thử lại."
                };
            }

            if (session.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _memoryCache.Remove(cacheKey);
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Mã OTP đã hết hạn. Vui lòng gửi lại mã xác nhận."
                };
            }

            var isOtpValid = BCrypt.Net.BCrypt.Verify(otp.Trim(), session.OtpHash);
            if (!isOtpValid)
            {
                session.FailedAttempts++;
                if (session.FailedAttempts >= MaxOtpAttempts)
                {
                    _memoryCache.Remove(cacheKey);
                    return new PatientRegistrationResult
                    {
                        Success = false,
                        Message = "Bạn đã nhập sai OTP quá nhiều lần. Vui lòng gửi lại mã xác nhận."
                    };
                }

                _memoryCache.Set(cacheKey, session, session.ExpiresAtUtc);
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Mã OTP không đúng. Vui lòng kiểm tra lại."
                };
            }

            var stillFree = await AssertPatientIdentifiersStillAvailableAsync(session);
            if (stillFree != null)
            {
                _memoryCache.Remove(cacheKey);
                return stillFree;
            }

            if (session.ExistingUserId.HasValue)
            {
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == session.ExistingUserId.Value);
                if (existingUser == null)
                {
                    _memoryCache.Remove(cacheKey);
                    return new PatientRegistrationResult
                    {
                        Success = false,
                        Message = "Không tìm thấy hồ sơ bệnh nhân để kích hoạt. Vui lòng liên hệ lễ tân."
                    };
                }

                if (existingUser.IsActive != true)
                {
                    _memoryCache.Remove(cacheKey);
                    return new PatientRegistrationResult
                    {
                        Success = false,
                        Message = "Tài khoản này đang bị khóa. Vui lòng liên hệ quản trị viên."
                    };
                }

                if (!CanClaimPreRegisteredPatient(existingUser))
                {
                    _memoryCache.Remove(cacheKey);
                    return new PatientRegistrationResult
                    {
                        Success = false,
                        Message = "Số điện thoại này đã thuộc tài khoản hoạt động. Vui lòng đăng nhập hoặc liên hệ lễ tân."
                    };
                }

                existingUser.Username = session.Username;
                existingUser.PasswordHash = session.PasswordHash;
                existingUser.FullName = session.FullName;
                existingUser.Email = session.Email;
                existingUser.PhoneNumber = session.Phone;
                if (!string.IsNullOrWhiteSpace(session.Address))
                {
                    existingUser.Address = session.Address;
                }

                if (session.Gender.HasValue)
                {
                    existingUser.Gender = session.Gender.Value;
                }

                if (session.DoB.HasValue)
                {
                    existingUser.DoB = session.DoB.Value;
                }

                existingUser.RoleMask |= PatientRoleMask;
            }
            else
            {
                var newUser = new User
                {
                    Username = session.Username,
                    PasswordHash = session.PasswordHash,
                    FullName = session.FullName,
                    Email = session.Email,
                    PhoneNumber = session.Phone,
                    Address = session.Address,
                    Gender = session.Gender,
                    DoB = session.DoB,
                    RoleMask = PatientRoleMask,
                    IsActive = true
                };

                _context.Users.Add(newUser);
            }

            await _context.SaveChangesAsync();

            _memoryCache.Remove(cacheKey);

            return new PatientRegistrationResult
            {
                Success = true,
                Message = "Đăng ký tài khoản bệnh nhân thành công. Vui lòng đăng nhập bằng thông tin vừa tạo.",
                AddedPatientRoleToExistingUser = false
            };
        }

        public async Task<PasswordResetResult> ResendPatientRegistrationOtpAsync(string email)
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

            var cacheKey = GetPatientRegistrationOtpCacheKey(normalizedEmail);
            if (!_memoryCache.TryGetValue(cacheKey, out PendingPatientRegistrationSession? session) || session == null)
            {
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "Phiên xác nhận không còn hiệu lực. Vui lòng điền lại form và gửi mã mới."
                };
            }

            var secondsSinceLastSend = (DateTime.UtcNow - session.LastSentAtUtc).TotalSeconds;
            if (secondsSinceLastSend < OtpResendCooldownSeconds)
            {
                var waitSeconds = OtpResendCooldownSeconds - (int)secondsSinceLastSend;
                return new PasswordResetResult
                {
                    Success = false,
                    Message = $"Vui lòng chờ {Math.Max(waitSeconds, 1)} giây trước khi gửi lại mã."
                };
            }

            var otp = GenerateOtp();
            session.OtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
            session.LastSentAtUtc = DateTime.UtcNow;
            session.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);
            session.FailedAttempts = 0;

            try
            {
                var subject = "[SmartClinic] Ma OTP xac nhan dang ky tai khoan benh nhan";
                var body = $"Xin chao,\n\n" +
                           $"Ma OTP xac nhan dang ky tai khoan benh nhan cua ban la: {otp}\n" +
                           $"Ma co hieu luc trong {OtpExpiryMinutes} phut.\n\n" +
                           "Neu ban khong yeu cau dang ky, vui long bo qua email nay.\n\n" +
                           "SmartClinic";
                await _emailService.SendEmailAsync(session.Email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resend patient registration OTP failed for email: {Email}", normalizedEmail);
                return new PasswordResetResult
                {
                    Success = false,
                    Message = "Không thể gửi lại OTP lúc này. Vui lòng thử lại sau."
                };
            }

            _memoryCache.Set(cacheKey, session, session.ExpiresAtUtc);

            return new PasswordResetResult
            {
                Success = true,
                Message = "Đã gửi lại mã OTP đến email của bạn."
            };
        }

        private async Task<PatientRegistrationResult> SendPatientRegistrationOtpAsyncInternal(
            PatientRegistrationRequest request,
            string normalizedUsername,
            string normalizedEmail,
            string normalizedPhone,
            int? existingUserId)
        {
            var cacheKey = GetPatientRegistrationOtpCacheKey(normalizedEmail);
            if (_memoryCache.TryGetValue(cacheKey, out PendingPatientRegistrationSession? existingPending) && existingPending != null)
            {
                var secondsSinceLastSend = (DateTime.UtcNow - existingPending.LastSentAtUtc).TotalSeconds;
                if (secondsSinceLastSend < OtpResendCooldownSeconds)
                {
                    var waitSeconds = OtpResendCooldownSeconds - (int)secondsSinceLastSend;
                    return new PatientRegistrationResult
                    {
                        Success = false,
                        Message = $"Bạn vừa yêu cầu mã. Vui lòng chờ {Math.Max(waitSeconds, 1)} giây hoặc kiểm tra hộp thư email (kể cả mục Spam)."
                    };
                }
            }

            var otp = GenerateOtp();
            var pendingSession = new PendingPatientRegistrationSession
            {
                ExistingUserId = existingUserId,
                Username = normalizedUsername,
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                Phone = normalizedPhone,
                Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                Gender = request.Gender,
                DoB = request.DoB,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                OtpHash = BCrypt.Net.BCrypt.HashPassword(otp),
                LastSentAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                FailedAttempts = 0
            };

            try
            {
                var subject = "[SmartClinic] Ma OTP xac nhan dang ky tai khoan benh nhan";
                var body = $"Xin chao {pendingSession.FullName},\n\n" +
                           $"Ma OTP xac nhan dang ky tai khoan benh nhan cua ban la: {otp}\n" +
                           $"Ma co hieu luc trong {OtpExpiryMinutes} phut.\n\n" +
                           "Neu ban khong yeu cau dang ky, vui long bo qua email nay.\n\n" +
                           "SmartClinic";
                await _emailService.SendEmailAsync(pendingSession.Email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Send patient registration OTP failed for email: {Email}", normalizedEmail);
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Không thể gửi email xác nhận lúc này. Vui lòng thử lại sau."
                };
            }

            _memoryCache.Set(cacheKey, pendingSession, pendingSession.ExpiresAtUtc);

            return new PatientRegistrationResult
            {
                Success = false,
                AwaitingEmailOtp = true,
                Message = $"Mã xác nhận đã gửi đến {pendingSession.Email}. Vui lòng nhập OTP để hoàn tất đăng ký."
            };
        }

        private async Task<PatientRegistrationResult?> AssertPatientIdentifiersStillAvailableAsync(
            PendingPatientRegistrationSession session)
        {
            var currentUserId = session.ExistingUserId;

            var emailTaken = await _context.Users.AnyAsync(u =>
                u.Email != null &&
                u.Email.Trim().ToLower() == session.Email &&
                (!currentUserId.HasValue || u.Id != currentUserId.Value));
            if (emailTaken)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Email vừa được đăng ký bởi tài khoản khác. Vui lòng bắt đầu lại quy trình đăng ký."
                };
            }

            var phoneOwners = await _context.Users.AsNoTracking()
                .Where(u => u.PhoneNumber != null && u.PhoneNumber != "")
                .Select(u => new { u.Id, u.PhoneNumber })
                .ToListAsync();
            var phoneDup = phoneOwners.Any(u =>
                (!currentUserId.HasValue || u.Id != currentUserId.Value) &&
                NormalizeVietnamPhoneDigits(u.PhoneNumber) == session.Phone);
            if (phoneDup)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Số điện thoại vừa được gắn với tài khoản khác. Vui lòng bắt đầu lại quy trình đăng ký."
                };
            }

            var usernameTaken = await _context.Users.AnyAsync(u =>
                u.Username.ToLower() == session.Username.ToLower() &&
                (!currentUserId.HasValue || u.Id != currentUserId.Value));
            if (usernameTaken)
            {
                return new PatientRegistrationResult
                {
                    Success = false,
                    Message = "Tên đăng nhập vừa được sử dụng. Vui lòng bắt đầu lại quy trình đăng ký."
                };
            }

            return null;
        }

        private static string GetPatientRegistrationOtpCacheKey(string normalizedEmail)
        {
            return $"patient-reg-otp:{normalizedEmail}";
        }

        private sealed class PendingPatientRegistrationSession
        {
            public string OtpHash { get; set; } = string.Empty;
            public DateTime ExpiresAtUtc { get; set; }
            public DateTime LastSentAtUtc { get; set; }
            public int FailedAttempts { get; set; }
            public int? ExistingUserId { get; set; }
            public string PasswordHash { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string? Address { get; set; }
            public bool? Gender { get; set; }
            public DateOnly? DoB { get; set; }
        }

        /// <summary>
        /// Thông báo khi tài khoản nội bộ (bác sĩ, lễ tân, ...) được gắn thêm quyền bệnh nhân — không đổi mật khẩu/email.
        /// </summary>
        private static string BuildStaffPatientActivationMessage(int nonPatientRoleMask)
        {
            var roleLabels = DescribeStaffRoleLabels(nonPatientRoleMask);
            var rolePhrase = string.IsNullOrEmpty(roleLabels)
                ? "tài khoản nội bộ"
                : $"tài khoản nội bộ của bạn (đang có vai trò: {roleLabels})";

            return $"Đã kích hoạt quyền bệnh nhân trên {rolePhrase}. " +
                   "Tên đăng nhập, mật khẩu và email trong hệ thống không thay đổi. " +
                   "Sau khi đăng nhập, bạn có thể dùng cùng tài khoản này khi đến khám với tư cách bệnh nhân.";
        }

        /// <summary>Chuẩn hóa số điện thoại VN để so sánh trùng: chỉ chữ số, +84/84 → 0.</summary>
        private static string NormalizeVietnamPhoneDigits(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var d = new string(raw.Trim().Where(char.IsDigit).ToArray());
            if (d.StartsWith("84") && d.Length >= 10)
                d = "0" + d[2..];
            return d;
        }

        private async Task<User?> FindUserByNormalizedPhoneAsync(string normalizedPhone)
        {
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                return null;
            }

            var phoneOwners = await _context.Users.AsNoTracking()
                .Where(u => u.PhoneNumber != null && u.PhoneNumber != "")
                .Select(u => new { u.Id, u.PhoneNumber })
                .ToListAsync();

            var ownerId = phoneOwners
                .FirstOrDefault(u => NormalizeVietnamPhoneDigits(u.PhoneNumber) == normalizedPhone)
                ?.Id;

            if (!ownerId.HasValue)
            {
                return null;
            }

            return await _context.Users.FirstOrDefaultAsync(u => u.Id == ownerId.Value);
        }

        private static bool CanClaimPreRegisteredPatient(User user)
        {
            var isPatient = (user.RoleMask & PatientRoleMask) == PatientRoleMask;
            if (!isPatient)
            {
                return false;
            }

            var hasEmail = !string.IsNullOrWhiteSpace(user.Email);
            var hasSystemGeneratedUsername = user.Username.StartsWith("walkin_", StringComparison.OrdinalIgnoreCase)
                || user.Username.StartsWith("patient_", StringComparison.OrdinalIgnoreCase);

            // Cho phép claim hồ sơ bệnh nhân do hệ thống/lễ tân tạo sẵn (thường username tạm, chưa có email).
            return hasSystemGeneratedUsername || !hasEmail;
        }

        private static string DescribeStaffRoleLabels(int mask)
        {
            if (mask == 0)
                return string.Empty;

            var parts = new List<string>();
            if ((mask & ReceptionRoleMask) == ReceptionRoleMask) parts.Add("lễ tân");
            if ((mask & DoctorRoleMask) == DoctorRoleMask) parts.Add("bác sĩ");
            if ((mask & PharmacistRoleMask) == PharmacistRoleMask) parts.Add("dược sĩ");
            if ((mask & CashierRoleMask) == CashierRoleMask) parts.Add("thu ngân");
            if ((mask & AdminRoleMask) == AdminRoleMask) parts.Add("quản trị viên");
            if ((mask & LabTechRoleMask) == LabTechRoleMask) parts.Add("kỹ thuật viên xét nghiệm");
            if ((mask & ManagerRoleMask) == ManagerRoleMask) parts.Add("quản lý");

            return string.Join(", ", parts);
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
                    Success = false,
                    Message = "Email không tồn tại trong hệ thống. Vui lòng kiểm tra lại."
                };
            }

            var cacheKey = GetOtpCacheKey(normalizedEmail);
            if (_memoryCache.TryGetValue(cacheKey, out PasswordResetOtpSession? existingSession) && existingSession != null)
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
            var secretKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Secret Key is not configured.");
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



        public async Task<PasswordResetResult> SendHistoryAccessOtpAsync(int ticketId, string email, string patientName)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return new PasswordResetResult { Success = false, Message = "Email bệnh nhân không tồn tại." };
            }

            var cacheKey = $"history-access-otp:{ticketId}";
            var otp = GenerateOtp();

            // Hash OTP in cache for security
            var otpHash = BCrypt.Net.BCrypt.HashPassword(otp);

            try
            {
                var subject = "[SmartClinic] Mã OTP yêu cầu xem hồ sơ bệnh án";
                var body = $"Xin chào {patientName},\n\n" +
                           $"Bác sĩ đang yêu cầu được xem lịch sử khám bệnh của bạn để phục vụ việc chăm sóc và tư vấn sức khỏe.\n\n" +
                           $"Mã OTP xác nhận của bạn là: {otp}\n" +
                           $"Mã này có hiệu lực trong {OtpExpiryMinutes} phút.\n\n" +
                           "Nếu bạn không phải là người đang thực hiện việc này, vui lòng bỏ qua email này để đảm bảo an toàn thông tin.\n\n" +
                           "Trân trọng,\n" +
                           "Đội ngũ SmartClinic";

                await _emailService.SendEmailAsync(normalizedEmail, subject, body);

                _memoryCache.Set(cacheKey, otpHash, TimeSpan.FromMinutes(OtpExpiryMinutes));

                return new PasswordResetResult { Success = true, Message = "OTP đã được gửi đến email bệnh nhân." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send History Access OTP for Ticket {TicketId}", ticketId);
                return new PasswordResetResult { Success = false, Message = "Lỗi gửi email: " + ex.Message };
            }
        }

        public async Task<bool> VerifyHistoryAccessOtpAsync(int ticketId, string otp)
        {
            var cacheKey = $"history-access-otp:{ticketId}";
            if (!_memoryCache.TryGetValue(cacheKey, out string? otpHash) || string.IsNullOrEmpty(otpHash))
            {
                return false;
            }

            var isValid = BCrypt.Net.BCrypt.Verify(otp.Trim(), otpHash);
            if (isValid)
            {
                _memoryCache.Remove(cacheKey); // Clear after success
            }
            return isValid;
        }

        private async Task<(int? RoomId, string? RoomName)> GetDoctorRoomContextAsync(User user)
        {
            if ((user.RoleMask & DoctorRoleMask) != DoctorRoleMask)
            {
                return (null, null);
            }

            var now = DateTime.Now;
            var today = now.Date;
            var currentTime = now.TimeOfDay;

            // Ưu tiên ca đang trực tại thời điểm đăng nhập.
            var activeShift = await _context.DoctorShifts
                .AsNoTracking()
                .Where(s =>
                    s.DoctorId == user.Id &&
                    s.Date == today &&
                    s.ShiftDefinition.StartTime <= currentTime &&
                    s.ShiftDefinition.EndTime >= currentTime)
                .OrderByDescending(s => s.ShiftDefinition.StartTime)
                .Select(s => new
                {
                    s.RoomId,
                    RoomName = s.Room.Name
                })
                .FirstOrDefaultAsync();

            if (activeShift != null)
            {
                return (activeShift.RoomId, activeShift.RoomName);
            }

            // Không có ca đang trực: lấy ca sắp diễn ra gần nhất trong ngày để giữ ngữ cảnh phòng.
            var upcomingShift = await _context.DoctorShifts
                .AsNoTracking()
                .Where(s =>
                    s.DoctorId == user.Id &&
                    s.Date == today &&
                    s.ShiftDefinition.StartTime > currentTime)
                .OrderBy(s => s.ShiftDefinition.StartTime)
                .Select(s => new
                {
                    s.RoomId,
                    RoomName = s.Room.Name
                })
                .FirstOrDefaultAsync();

            if (upcomingShift != null)
            {
                return (upcomingShift.RoomId, upcomingShift.RoomName);
            }

            // Fallback cuối: lấy ca gần nhất trước đó (nếu bác sĩ chỉ còn ca đã qua trong ngày).
            var latestShift = await _context.DoctorShifts
                .AsNoTracking()
                .Where(s => s.DoctorId == user.Id && s.Date == today)
                .OrderByDescending(s => s.ShiftDefinition.StartTime)
                .Select(s => new
                {
                    s.RoomId,
                    RoomName = s.Room.Name
                })
                .FirstOrDefaultAsync();

            return latestShift is null
                ? (null, null)
                : (latestShift.RoomId, latestShift.RoomName);
        }

        private sealed class PasswordResetOtpSession
        {
            public int UserId { get; set; }
            public string OtpHash { get; set; } = string.Empty;
            public DateTime ExpiresAtUtc { get; set; }
            public DateTime LastSentAtUtc { get; set; }
            public int FailedAttempts { get; set; }
        }

        public string GetRedirectUrl(int roleMask)
        {
            if ((roleMask & AdminRoleMask) == AdminRoleMask) return "/admin/daily-revenue";
            if ((roleMask & DoctorRoleMask) == DoctorRoleMask) return "/doctor/dashboard";
            if ((roleMask & PatientRoleMask) == PatientRoleMask) return "/patient/medical-history";
            if ((roleMask & ReceptionRoleMask) == ReceptionRoleMask) return "/checkin";
            if ((roleMask & LabTechRoleMask) == LabTechRoleMask) return "/lab";
            if ((roleMask & CashierRoleMask) == CashierRoleMask) return "/cashier/payments";
            if ((roleMask & PharmacistRoleMask) == PharmacistRoleMask) return "/pharmacist/prescriptions";

            return "/";
        }
    }
}