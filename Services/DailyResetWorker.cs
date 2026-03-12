using Microsoft.EntityFrameworkCore;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class DailyResetWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyResetWorker> _logger;

        // TẠI SAO LẠI LÀ IServiceProvider MÀ KHÔNG PHẢI SmartClinicDbContext?
        // (Xem giải thích ở phần Dưới)
        public DailyResetWorker(IServiceProvider serviceProvider, ILogger<DailyResetWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DailyResetWorker đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Tính toán thời gian ngủ (Delay)
                var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                var nowInVietnam = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
                var nextMidnightInVietnam = nowInVietnam.Date.AddDays(1); // Lấy 00:00:00 của ngày hôm sau
                var timeUntilMidnight = nextMidnightInVietnam - nowInVietnam;

                _logger.LogInformation($"Sẽ reset số thứ tự sau: {timeUntilMidnight.Hours} giờ {timeUntilMidnight.Minutes} phút.");

                // 2. Đi ngủ và chờ đến đúng 00:00 (truyền thêm stoppingToken để có thể dừng an toàn khi tắt app)
                await Task.Delay(timeUntilMidnight, stoppingToken);

                // 3. Đúng 00:00 thì thức dậy làm việc!
                if (!stoppingToken.IsCancellationRequested)
                {
                    await ResetSequenceAsync();
                }
            }
        }

        private async Task ResetSequenceAsync()
        {
            _logger.LogInformation("Bắt đầu reset Sequence về 1...");

            // Phải tạo một Scope mới để lấy DbContext ra dùng
            using (var scope = _serviceProvider.CreateScope())
            {
                // Lấy DbContext từ Scope
                var dbContext = scope.ServiceProvider.GetRequiredService<SmartClinicDbContext>();

                try
                {
                    // Chạy lệnh Raw SQL để reset chuỗi Sequence
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER SEQUENCE TicketNumberSeq RESTART WITH 1");
                    _logger.LogInformation("ĐÃ RESET THÀNH CÔNG SỐ THỨ TỰ CHO NGÀY MỚI.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi thảm họa khi reset Sequence!");
                }
            }
        }
    }
}
