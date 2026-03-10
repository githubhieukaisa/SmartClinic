using Blazored.Toast;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Components;
using SmartClinic.Hubs;
using SmartClinic.Models;
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
            builder.Services.AddDbContext<SmartClinicDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("MyCnn")));
            
            builder.Services.AddSignalR();

            //Đăng ký service class
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddScoped<IDepartmentService, DepartmentService>();

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
