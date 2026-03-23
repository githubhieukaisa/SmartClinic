using Blazored.Toast;
using Blazored.Toast.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// Đăng ký DbContext với PostgreSQL
        /// Dùng AddDbContextFactory để cho phép tạo fresh DbContext instance mỗi lần
        /// Perfect cho SignalR callbacks và async operations
        /// </summary>
        public static IServiceCollection AddSmartClinicDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            // ✅ Đăng ký CÁCH 1: DbContextFactory (cho services)
            services.AddDbContextFactory<SmartClinicDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("MyCnn")));

            // ✅ Đăng ký CÁCH 2: DbContext bằng Pool (cho pages)
            // Tạo pool từ factory để pages có thể inject DbContext trực tiếp
            services.AddPooledDbContextFactory<SmartClinicDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("MyCnn")));

            return services;
        }

        /// <summary>
        /// Đăng ký các business logic services (Ticket, Department, Auth, Queue)
        /// </summary>
        public static IServiceCollection AddSmartClinicServices(this IServiceCollection services)
        {
            services.AddScoped<ITicketService, TicketService>();
            services.AddScoped<IDepartmentService, DepartmentService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IQueueService, QueueService>();
            services.AddScoped<PatientService>();
            services.AddScoped<IPharmacyService, PharmacyService>();
            services.AddScoped<ICashierService, CashierService>();
            services.AddScoped<ILabService, LabService>();
            services.AddSingleton<NotificationService>();
             services.AddScoped<VNPayService>();
            services.AddHttpContextAccessor();
            services.AddBlazoredToast();
            //Đăng ký logger cho AuthService

            return services;
        }

        /// <summary>
        /// Cấu hình Hangfire với PostgreSQL
        /// </summary>
        public static IServiceCollection AddSmartClinicHangfire(this IServiceCollection services, IConfiguration configuration)
        {
            var hangfireConn = configuration.GetConnectionString("HangfireDb");

            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options =>
                {
                    options.UseNpgsqlConnection(hangfireConn);
                }, new PostgreSqlStorageOptions
                {
                    PrepareSchemaIfNecessary = true 
                }));

            services.AddHangfireServer(options =>
            {
                options.WorkerCount = 2;
            });

            return services;
        }

        /// <summary>
        /// Cấu hình Cookie Authentication với token renewal logic
        /// </summary>
        public static IServiceCollection AddCustomAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
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
                            if (expiresUtc != null && expiresUtc.Value < DateTimeOffset.UtcNow)
                            {
                                var refreshToken = context.Properties.GetString("refresh_token");
                                if (!string.IsNullOrEmpty(refreshToken))
                                {
                                    // change token
                                    var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                                    var newTokens = await authService.RenewTokenAsync(refreshToken);

                                    if (newTokens != null)
                                    {
                                        context.Properties.StoreTokens(new[] {
                                                new AuthenticationToken { Name = "access_token", Value = newTokens.AccessToken },
                                                new AuthenticationToken { Name = "refresh_token", Value = newTokens.RefreshToken }
                                            });

                                        context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15);
                                        //Save new cookie
                                        context.ShouldRenew = true;
                                    }
                                    else
                                    {
                                        context.RejectPrincipal();
                                        await context.HttpContext.SignOutAsync();
                                    }
                                }
                            }
                        }
                    };
                });

            services.AddCascadingAuthenticationState();
            return services;
        }

        /// <summary>
        /// Cấu hình Authorization policies dựa trên Role Mask
        /// </summary>
        public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
        {
            services.AddAuthorizationCore(options =>
            {
                options.AddPolicy("ReceptionPolicy", policy => policy.RequireAssertion(context => HasRole(context, 1)));
                options.AddPolicy("DoctorPolicy", policy => policy.RequireAssertion(context => HasRole(context, 2)));
                options.AddPolicy("PharmacistPolicy", policy => policy.RequireAssertion(context => HasRole(context, 4)));
                options.AddPolicy("CashierPolicy", policy => policy.RequireAssertion(context => HasRole(context, 8)));
                options.AddPolicy("AdminPolicy", policy => policy.RequireAssertion(context => HasRole(context, 16)));
                options.AddPolicy("LabTechPolicy", policy => policy.RequireAssertion(context => HasRole(context, 32)));
            });
            return services;
        }

        /// <summary>
        /// Helper method để kiểm tra role dựa trên RoleMask claim
        /// </summary>
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
