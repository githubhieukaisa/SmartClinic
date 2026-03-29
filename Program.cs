using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Components;
using SmartClinic.Hubs;
using SmartClinic.Models;
using SmartClinic.Constant;
using SmartClinic.Security;
using SmartClinic.Services;
using Radzen;

namespace SmartClinic
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddSignalR();
            
            // Fix Antiforgery cross-site mismatch cookie errors (e.g. from VNPay return page bookmarks)
            builder.Services.AddAntiforgery(options => 
            {
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            // Register database
            builder.Services.AddSmartClinicDatabase(builder.Configuration);

            // Register business logic services
            builder.Services.AddSmartClinicServices();

            // Register Radzen chart components
            builder.Services.AddRadzenComponents();

            // Register Hangfire
            builder.Services.AddSmartClinicHangfire(builder.Configuration);

            // Register Authentication and Authorization
            builder.Services.AddCustomAuthentication();
            builder.Services.AddCustomAuthorization();

            var app = builder.Build();

            // ── One-time data fix: cập nhật RemainCapacity cho DoctorShift cũ ──
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SmartClinicDbContext>();
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE \"DoctorShifts\" SET \"RemainCapacity\" = \"Capacity\" WHERE \"RemainCapacity\" = 0");

                // --- Seed Sample Users (Pharmacist, Cashier, Patients) ---
                var hasPharmacist = await db.Users.AnyAsync(u => u.Username == "pharmacist");
                if (!hasPharmacist)
                {
                    db.Users.Add(new User
                    {
                        Username = "pharmacist",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                        FullName = "Dược sĩ Demo",
                        PhoneNumber = "0987654321",
                        RoleMask = 4, // PharmacistRoleMask
                        IsActive = true
                    });
                }

                var hasCashier = await db.Users.AnyAsync(u => u.Username == "cashier");
                if (!hasCashier)
                {
                    db.Users.Add(new User
                    {
                        Username = "cashier",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                        FullName = "Thu ngân Demo",
                        PhoneNumber = "0987654322",
                        RoleMask = 8, // CashierRoleMask
                        IsActive = true
                    });
                }

                var hasPatient1 = await db.Users.AnyAsync(u => u.Username == "0987654323");
                if (!hasPatient1)
                {
                    db.Users.Add(new User
                    {
                        Username = "0987654323", // Patient username is often phone number
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                        FullName = "Bệnh nhân Demo 1",
                        PhoneNumber = "0987654323",
                        RoleMask = 128, // PatientRoleMask
                        IsActive = true,
                        Gender = true
                    });
                }

                var hasPatient2 = await db.Users.AnyAsync(u => u.Username == "0987654324");
                if (!hasPatient2)
                {
                    db.Users.Add(new User
                    {
                        Username = "0987654324",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                        FullName = "Bệnh nhân Demo 2",
                        PhoneNumber = "0987654324",
                        RoleMask = 128, // PatientRoleMask
                        IsActive = true,
                        Gender = false
                    });
                }

                await db.SaveChangesAsync();

                // --- Seed Medicines for Demo ---
                if (!await db.Medicines.AnyAsync())
                {
                    var med1 = new Medicine { Name = "Paracetamol 500mg", Unit = "Viên", StockQuantity = 1000 };
                    var med2 = new Medicine { Name = "Amoxicillin 250mg", Unit = "Viên", StockQuantity = 500 };
                    db.Medicines.AddRange(med1, med2);
                    await db.SaveChangesAsync();

                    db.MedicinePrices.AddRange(
                        new MedicinePrice { MedicineId = med1.Id, Price = 5000, EffectiveFrom = DateTime.UtcNow.AddYears(-1) },
                        new MedicinePrice { MedicineId = med2.Id, Price = 15000, EffectiveFrom = DateTime.UtcNow.AddYears(-1) }
                    );
                    await db.SaveChangesAsync();
                }

                // --- Seed Queue Tickets & Prescriptions for Demo ---
                var pt1 = await db.Users.FirstOrDefaultAsync(u => u.Username == "0987654323");
                var roomDoc = await db.Rooms.FirstOrDefaultAsync(r => r.Name == "Phòng Khám Nội 01");
                var defaultRoomId = roomDoc?.Id ?? 9;

                if (pt1 != null && !await db.QueueTickets.AnyAsync(t => t.PatientId == pt1.Id))
                {
                    var med1 = await db.Medicines.FirstOrDefaultAsync();

                    // 1. Ticket For Pharmacist (Status: Examinating, Prescription: Pending)
                    var ticketPharma = new QueueTicket
                    {
                        PatientId = pt1.Id,
                        TicketNumber = 105,
                        StatusEnum = TicketStatus.Examinating,
                        CreatedAt = DateTime.UtcNow,
                        RoomId = defaultRoomId
                    };
                    db.QueueTickets.Add(ticketPharma);
                    await db.SaveChangesAsync();

                    var rx1 = new Prescription
                    {
                        TicketId = ticketPharma.Id,
                        Status = PrescriptionStatus.Pending,
                        DoctorNote = "Cần uống nhiều nước",
                        CreatedAt = DateTime.UtcNow,
                        TotalAmount = 50000
                    };
                    db.Prescriptions.Add(rx1);
                    await db.SaveChangesAsync();

                    if (med1 != null)
                    {
                        db.PrescriptionDetails.Add(new PrescriptionDetail
                        {
                            PrescriptionId = rx1.Id,
                            MedicineId = med1.Id,
                            Quantity = 10,
                            UnitPrice = 5000,
                            UsageInstruction = "Sáng 1 viên, Tối 1 viên"
                        });
                        await db.SaveChangesAsync();
                    }

                    // 2. Ticket For Cashier (Status: Completed, Prescription: Dispensed/Done)
                    var ticketCashier = new QueueTicket
                    {
                        PatientId = pt1.Id,
                        TicketNumber = 106,
                        StatusEnum = TicketStatus.Completed, // Ready for Payment
                        CreatedAt = DateTime.UtcNow,
                        TotalAmount = null,
                        RoomId = defaultRoomId
                    };
                    db.QueueTickets.Add(ticketCashier);
                    await db.SaveChangesAsync();

                    var rx2 = new Prescription
                    {
                        TicketId = ticketCashier.Id,
                        Status = PrescriptionStatus.Dispensed, // Pharmacist finished
                        DoctorNote = "Kiêng đồ dầu mỡ",
                        CreatedAt = DateTime.UtcNow,
                        TotalAmount = 50000 
                    };
                    db.Prescriptions.Add(rx2);
                    await db.SaveChangesAsync();

                    if (med1 != null)
                    {
                        db.PrescriptionDetails.Add(new PrescriptionDetail
                        {
                            PrescriptionId = rx2.Id,
                            MedicineId = med1.Id,
                            Quantity = 10,
                            UnitPrice = 5000,
                            UsageInstruction = "Sáng 1 viên"
                        });
                        await db.SaveChangesAsync();
                    }

                    // 3. Historical Payment for Cashier History Demo
                    var ticketHistory = new QueueTicket
                    {
                        PatientId = pt1.Id,
                        TicketNumber = 101,
                        StatusEnum = TicketStatus.Done, 
                        CreatedAt = DateTime.UtcNow.AddHours(-5),
                        TotalAmount = 75000,
                        RoomId = defaultRoomId
                    };
                    db.QueueTickets.Add(ticketHistory);
                    await db.SaveChangesAsync();

                    var cashier = await db.Users.FirstOrDefaultAsync(u => u.Username == "cashier");
                    db.Payments.Add(new SmartClinic.Models.Entites.Payment
                    {
                        TicketId = ticketHistory.Id,
                        PaymentMethod = "Cash",
                        TotalAmount = 75000,
                        AmountReceived = 100000,
                        ChangeAmount = 25000,
                        CashierId = cashier?.Id,
                        PaymentTime = DateTime.UtcNow.AddHours(-4),
                        Status = "Success"
                    });
                    await db.SaveChangesAsync();
                }
            }
            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.MapHub<PatientHub>("/hubs/patient");
            app.MapHub<PrescriptionHub>("/hubs/prescription");


            app.MapHub<QueueHub>("/queueHub");
            app.MapHub<LabHub>("/labHub");

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseAntiforgery();

            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                DashboardTitle = "SmartClinic Background Jobs",

                // Authorization filter for admin access
                Authorization = new[] { new MyHangfireAuthorizationFilter() },

                AppPath = "/login"
            });

            // Logout endpoint để xóa cookie và đăng xuất người dùng
            app.MapPost("/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Cookies.Delete("jwt_token");
                context.Response.Cookies.Delete("refresh_token");
                return Results.Redirect("/login");
            });
            app.MapGet("/logout-action", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Redirect("/login");
            });

            // ── VNPay callback endpoint ─────────────────────────────────────────
            // Dùng minimal API (không qua Blazor) để tránh lỗi antiforgery + auth cookie
            // khi VNPay redirect cross-site về app.
            // Trả HTML + JS thay vì 302 Redirect để cắt chuỗi cross-site redirect,
            // đảm bảo trình duyệt gửi auth cookie cho navigation tiếp theo.
            app.MapGet("/api/vnpay-return", async (HttpContext context, ICashierService cashierService) =>
            {
                var result = await cashierService.HandleVNPayCallbackAsync(context.Request.Query);

                string redirectUrl;
                if (result.Success || (result.ErrorMessage != null && result.ErrorMessage.Contains("'Done'")))
                {
                    var msg = Uri.EscapeDataString("Thanh toán VNPay thành công!");
                    redirectUrl = $"/cashier/payments?vnpay_success=true&vnpay_msg={msg}";
                }
                else
                {
                    var msg = Uri.EscapeDataString(result.ErrorMessage ?? "Thanh toán thất bại");
                    redirectUrl = $"/cashier/payments?vnpay_error=true&vnpay_msg={msg}";
                }

                // Trả HTML + JS để tạo navigation mới (same-site), không dùng 302 redirect
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync($"""
                    <!DOCTYPE html>
                    <html>
                    <head><meta charset="utf-8"><title>Đang chuyển hướng...</title></head>
                    <body style="display:flex;align-items:center;justify-content:center;height:100vh;font-family:sans-serif;background:#f8fafc">
                        <p>Đang chuyển hướng về trang thanh toán...</p>
                        <script>window.location.replace('{redirectUrl}');</script>
                    </body>
                    </html>
                """);
            });

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            RecurringJob.AddOrUpdate<SequenceResetJob>(
                "daily-sequence-reset", // job id
                job => job.ExecuteAsync(), // Hàm sẽ được gọi
                "0 0 * * *", // Cron expression: 0 phút, 0 giờ (Nửa đêm)
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
                });

            RecurringJob.AddOrUpdate<WeeklyScheduleReminderJob>(
                "weekly-schedule-reminder", 
                job => job.ExecuteAsync(), 
                "0 17 * * 5", // 17:00 Thứ 6 hàng tuần
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
                });

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
