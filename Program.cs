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
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddSignalR();

            builder.Services.AddHostedService<DailyResetWorker>();

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

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
