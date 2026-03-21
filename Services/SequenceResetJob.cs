using Microsoft.EntityFrameworkCore;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class SequenceResetJob
    {
        private readonly SmartClinicDbContext _dbContext;
        private readonly ILogger<SequenceResetJob> _logger;

        public SequenceResetJob(SmartClinicDbContext dbContext, ILogger<SequenceResetJob> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Hangfire: Bắt đầu reset Sequence về 1...");
            try
            {
                // Lưu ý: PostgreSQL thường phân biệt hoa thường với tên có dấu ngoặc kép
                await _dbContext.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"TicketNumberSeq\" RESTART WITH 1");
                _logger.LogInformation("Hangfire: ĐÃ RESET THÀNH CÔNG SỐ THỨ TỰ CHO NGÀY MỚI.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi reset Sequence qua Hangfire!");
                throw; // BẮT BUỘC ném lỗi ra ngoài để Hangfire biết Job thất bại và tự động Retry
            }
        }
    }
}
