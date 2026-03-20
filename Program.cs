using Hangfire;
using SmartClinic.Components;
using SmartClinic.Hubs;
using SmartClinic.Models;
using SmartClinic.Security;
using SmartClinic.Services;

namespace SmartClinic
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddSignalR();

            // Register database
            builder.Services.AddSmartClinicDatabase(builder.Configuration);

            // Register business logic services
            builder.Services.AddSmartClinicServices();

            // Register Hangfire
            builder.Services.AddSmartClinicHangfire(builder.Configuration);

            // Register Authentication and Authorization
            builder.Services.AddCustomAuthentication();
            builder.Services.AddCustomAuthorization();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.MapHub<PatientHub>("/hubs/patient");

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

            //RecurringJob.AddOrUpdate<SequenceResetJob>(
            //    "daily-sequence-reset", // ID của Job (đặt tên tùy ý)
            //    job => job.ExecuteAsync(), // Hàm sẽ được gọi
            //    "0 0 * * *", // Cron expression: 0 phút, 0 giờ (Nửa đêm)
            //    new RecurringJobOptions
            //    {
            //        // Fix triệt để lỗi sai múi giờ trên Server
            //        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
            //    });

            //app.MapPost("/api/test-checkin", async (ITicketService ticketService) =>
            //{
            //    try
            //    {
            //        // Giả lập 100 user khác nhau cùng vào Khoa Nội (DepartmentId = 1)
            //        var randomPhone = $"09{Random.Shared.Next(10000000, 99999999)}";
            //        var ticket = await ticketService.GenerateTicketAsync("Test Load", randomPhone, 1);
            //        return Results.Ok(new { ticket.TicketNumber, ticket.RoomId });
            //    }
            //    catch (Exception ex)
            //    {
            //        return Results.BadRequest(ex.Message);
            //    }
            //});

            app.MapGet("/api/tts", async (string text) =>
            {
                // Gọi cổng tw-ob cực xịn của Google
                string url = $"https://translate.google.com/translate_tts?ie=UTF-8&tl=vi-VN&client=tw-ob&q={Uri.EscapeDataString(text)}";

                using var client = new HttpClient();
                // Giả danh trình duyệt để Google không chặn
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                // Tải file MP3 về Server C#
                var audioBytes = await client.GetByteArrayAsync(url);

                // Trả file MP3 đó về cho Tivi Frontend của mình
                return Results.File(audioBytes, "audio/mpeg");
            });

            app.Run();
        }
    }
}
