using Blazored.Toast;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Models;
using SmartClinic.Hubs;
using SmartClinic.Components;
using SmartClinic.Services;

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
            
            // ✅ Use AddDbContextFactory instead of AddDbContext
            // This allows creating fresh DbContext instances per operation
            // Perfect for SignalR callbacks and async operations
            builder.Services.AddDbContextFactory<SmartClinicDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("MyCnn")));
            
            builder.Services.AddSignalR();
            
            builder.Services.AddScoped<PatientService>();
            
            // ✅ Register NotificationService as Singleton
            // This ensures only ONE connection per user session
            // All pages share the same connection instance
            // Connection is initialized GLOBALLY below (not tied to any page)
            builder.Services.AddSingleton<NotificationService>();
            
            //Đăng ký service class
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddScoped<IDepartmentService, DepartmentService>();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<IAuthService, AuthService>();

            // 1. ĐĂNG KÝ HANGFIRE VÀ KẾT NỐI DB POSTGRESQL
            builder.Services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options =>
                    options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("MyCnn"))));

            // 2. KHỞI ĐỘNG External services
            builder.Services.AddHangfireServer();
            builder.Services.AddBlazoredToast();

            // Đăng ký QueueService để có thể Inject vào TicketService và SequenceResetJob
            builder.Services.AddScoped<IQueueService, QueueService>();
            builder.Services.AddBlazoredToast();

            var app = builder.Build();
            
            // ============================================================================
            // ✅ GLOBAL SIGNALR INITIALIZATION (Fire-and-Forget)
            // ============================================================================
            // Start the SignalR connection globally when app starts
            // Does NOT block app startup - runs async in background
            // Connection persists across all pages and components
            // This replaces per-page initialization in MyPatient.razor
            _ = app.Services.GetRequiredService<NotificationService>().EnsureStartedAsync();
            System.Diagnostics.Debug.WriteLine("[Program] ✅ Global SignalR initialization started (fire-and-forget)");

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            
            // ✅ Map the SignalR hub
            app.MapHub<PatientHub>("/hubs/patient");

            app.MapHub<QueueHub>("/queueHub");

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.UseHangfireDashboard("/hangfire");

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
    }
}


