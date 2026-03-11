using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SmartClinic.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ProtectedLocalStorage _localStorage;
        private readonly IServiceScopeFactory _scopeFactory;
        private const string AccessTokenKey = "access_token";
        private const string RefreshTokenKey = "refresh_token";

        public CustomAuthStateProvider(ProtectedLocalStorage localStorage, IServiceScopeFactory scopeFactory)
        {
            _localStorage = localStorage;
            _scopeFactory = scopeFactory;
        }

        // HÀM QUAN TRỌNG NHẤT: Blazor sẽ gọi hàm này liên tục để kiểm tra user là ai
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // Cố gắng đọc token đã được mã hóa bảo mật từ LocalStorage
                var tokenResult = await _localStorage.GetAsync<string>(AccessTokenKey);
                var token = tokenResult.Success ? tokenResult.Value : null;

                if (string.IsNullOrWhiteSpace(token))
                    return BuildAnonymousState();

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // KIỂM TRA HẾT HẠN (SILENT RENEW)
                if (jwtToken.ValidTo < DateTime.UtcNow)
                {
                    var refreshResult = await _localStorage.GetAsync<string>(RefreshTokenKey);
                    var refreshToken = refreshResult.Success ? refreshResult.Value : null;

                    if (string.IsNullOrWhiteSpace(refreshToken))
                        return BuildAnonymousState(); // Không có refresh token -> Bắt đăng nhập lại

                    // Tạo một Scope mới để gọi DbContext an toàn trong Blazor Server
                    using var scope = _scopeFactory.CreateScope();
                    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

                    // Gọi hàm đổi Token
                    var newTokens = await authService.RenewTokenAsync(refreshToken);

                    if (newTokens == null)
                    {
                        await MarkUserAsLoggedOut(); // Refresh Token đã hết hạn 7 ngày hoặc bị hack -> Đuổi ra ngoài
                        return BuildAnonymousState();
                    }

                    // Lưu lại Token mới và cập nhật biến nội bộ để đi tiếp
                    await _localStorage.SetAsync(AccessTokenKey, newTokens.AccessToken);
                    await _localStorage.SetAsync(RefreshTokenKey, newTokens.RefreshToken);

                    jwtToken = handler.ReadJwtToken(newTokens.AccessToken);
                }

                // Trả về User hợp lệ
                var identity = new ClaimsIdentity(jwtToken.Claims, "jwtAuthType");
                return new AuthenticationState(new ClaimsPrincipal(identity));
            }
            catch
            {
                return BuildAnonymousState();
            }
        }

        private AuthenticationState BuildAnonymousState()
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Gọi hàm này sau khi chạy AuthService.LoginAsync thành công
        public async Task MarkUserAsAuthenticated(string accessToken, string refreshToken)
        {
            // Lưu token vào storage bọc thép
            await _localStorage.SetAsync(AccessTokenKey, accessToken);
            await _localStorage.SetAsync(RefreshTokenKey, refreshToken);

            // Bóc tách token để lấy thông tin
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(accessToken);
            var identity = new ClaimsIdentity(jwtToken.Claims, "jwtAuthType");
            var user = new ClaimsPrincipal(identity);

            // PHÁT LOA THÔNG BÁO CHO TOÀN BỘ UI BIẾT CÓ NGƯỜI VỪA ĐĂNG NHẬP (UI sẽ tự động đổi)
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        // Gọi hàm này khi user bấm nút Đăng Xuất
        public async Task MarkUserAsLoggedOut()
        {
            await _localStorage.DeleteAsync(AccessTokenKey);
            await _localStorage.DeleteAsync(RefreshTokenKey);

            var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());

            // PHÁT LOA THÔNG BÁO ĐÃ ĐĂNG XUẤT
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
        }
    }
}