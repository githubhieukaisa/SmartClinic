using Microsoft.EntityFrameworkCore;
using SmartClinic.Web;
using SmartClinic.Models;
using SmartClinic.Hubs;
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
            builder.Services.AddSingleton<NotificationService>();
            
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            
            // ✅ Map the SignalR hub
            app.MapHub<PatientHub>("/hubs/patient");

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();
            
            app.Run();
        }
    }
}


