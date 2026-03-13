using Blazored.Toast;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartClinic.Components;
using SmartClinic.Hubs;
using SmartClinic.Models;
using SmartClinic.Security;
using SmartClinic.Services;
using System.Text;

namespace SmartClinic
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddDbContext<SmartClinicDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("MyCnn")));
            builder.Services.AddSignalR();

            //Đăng ký service
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddScoped<IDepartmentService, DepartmentService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IQueueService, QueueService>();
            builder.Services.AddBlazoredToast();

            //Đăng ký jwt authentication
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options=>
            {
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/login";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;

                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = async context =>
                    {
                        var expiresUtc = context.Properties.ExpiresUtc;
                        // Kiểm tra xem Access Token đã hết hạn 15 phút chưa?
                        if (expiresUtc != null && expiresUtc.Value < DateTimeOffset.UtcNow)
                        {
                            var refreshToken = context.Properties.GetString("refresh_token");
                            if (!string.IsNullOrEmpty(refreshToken))
                            {
                                // Hết hạn -> Gọi AuthService đổi Token mới!
                                var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                                var newTokens = await authService.RenewTokenAsync(refreshToken);

                                if (newTokens != null)
                                {
                                    // Lưu Token mới vào Cookie nội bộ
                                    context.Properties.StoreTokens(new[] {
                                            new AuthenticationToken { Name = "access_token", Value = newTokens.AccessToken },
                                            new AuthenticationToken { Name = "refresh_token", Value = newTokens.RefreshToken }
                                        });

                                    // Gia hạn thời gian sống thêm 15 phút nữa
                                    context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15);

                                    // Ra lệnh cho hệ thống tự động lưu Cookie mới xuống trình duyệt
                                    context.ShouldRenew = true;
                                }
                                else
                                {
                                    // Refresh token hỏng -> Hủy phiên, bắt đăng nhập lại
                                    context.RejectPrincipal();
                                    await context.HttpContext.SignOutAsync();
                                }
                            }
                        }
                    }
                };
            });

            builder.Services.AddCascadingAuthenticationState();
            

            // 1. ĐĂNG KÝ HANGFIRE VÀ KẾT NỐI DB POSTGRESQL
            builder.Services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options =>
                    options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("MyCnn"))));

            // 2. KHỞI ĐỘNG External services
            builder.Services.AddHangfireServer();

            builder.Services.AddAuthorizationCore(options =>
            {
                options.AddPolicy("ReceptionPolicy", policy => policy.RequireAssertion(context => HasRole(context, 1)));
                options.AddPolicy("DoctorPolicy", policy => policy.RequireAssertion(context => HasRole(context, 2)));
                options.AddPolicy("PharmacistPolicy", policy => policy.RequireAssertion(context => HasRole(context, 4)));
                options.AddPolicy("CashierPolicy", policy => policy.RequireAssertion(context => HasRole(context, 8)));
                options.AddPolicy("AdminPolicy", policy => policy.RequireAssertion(context => HasRole(context, 16)));
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.MapHub<QueueHub>("/queueHub");

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseAntiforgery();

            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                DashboardTitle = "SmartClinic Background Jobs",

                // Gắn cái Khiên bảo vệ bạn vừa tạo vào đây
                Authorization = new[] { new MyHangfireAuthorizationFilter() },

                AppPath = "/login"
            });

            // Định nghĩa route cho logout
            app.MapPost("/logout", (HttpContext context) =>
            {
                context.Response.Cookies.Delete("jwt_token");
                context.Response.Cookies.Delete("refresh_token");
                return Results.Redirect("/login");
            });

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            RecurringJob.AddOrUpdate<SequenceResetJob>(
                "daily-sequence-reset", // ID của Job (đặt tên tùy ý)
                job => job.ExecuteAsync(), // Hàm sẽ được gọi
                "0 0 * * *", // Cron expression: 0 phút, 0 giờ (Nửa đêm)
                new RecurringJobOptions
                {
                    // Fix triệt để lỗi sai múi giờ trên Server
                    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
                });

            app.Run();
        }

        private static bool HasRole(AuthorizationHandlerContext context, int targetRoleMask)
        {
            var roleMaskClaim = context.User.FindFirst("RoleMask")?.Value;
            if (int.TryParse(roleMaskClaim, out int roleMask))
            {
                return (roleMask & targetRoleMask) == targetRoleMask;
            }
            return false;
        }
    }
}
