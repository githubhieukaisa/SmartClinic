using Microsoft.EntityFrameworkCore;
using SmartClinic.DTOs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class QueueService : IQueueService
    {
        private readonly SmartClinicDbContext _context;

        public QueueService(SmartClinicDbContext context)
        {
            _context = context;
        }

        public async Task<QueueDisplayDto> GetDisplayDataAsync()
        {
            // 1. Tìm số đang được gọi vào phòng (Giả sử Status là "Calling")
            // Nếu hệ thống của bạn lưu Status khác, hãy đổi lại cho khớp nhé!
            var currentCall = await _context.QueueTickets
                .Where(t => t.Status == "Calling")
                .OrderByDescending(t => t.CreatedAt) // Lấy người mới được gọi nhất
                .FirstOrDefaultAsync();

            // 2. Tìm danh sách 5 số tiếp theo đang chờ (Status = "Waiting")
            var nextTickets = await _context.QueueTickets
                .Where(t => t.Status == "Waiting")
                .OrderBy(t => t.TicketNumber) // Xếp theo số thứ tự từ nhỏ đến lớn
                .Take(5)
                .Select(t => t.TicketNumber.ToString())
                .ToListAsync();

            // 3. Đóng gói trả về DTO cho màn hình
            return new QueueDisplayDto
            {
                CurrentTicketNumber = currentCall?.TicketNumber.ToString() ?? "---",
                RoomName = currentCall != null ? "Phòng Khám 1" : "---", // Tạm fix cứng, sau này có thể lấy từ DB
                NextTickets = nextTickets
            };
        }
    }
}
