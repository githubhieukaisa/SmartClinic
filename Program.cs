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
            
            // ✅ Register ToastNotificationService as Scoped
            // Simple JavaScript-based toast notifications
            // Independent of Blazor component lifecycle
            builder.Services.AddScoped<ToastNotificationService>();
            
            
            // ✅ Register NotificationService as Singleton
            // This ensures only ONE connection per user session
            // All pages share the same connection instance
            // Connection is initialized GLOBALLY below (not tied to any page)
            builder.Services.AddSingleton<NotificationService>();
            
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

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();
            
            app.Run();
        }
    }
}


