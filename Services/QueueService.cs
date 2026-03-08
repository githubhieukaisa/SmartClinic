using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.DTOs;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class QueueService : IQueueService
    {
        private readonly SmartClinicDbContext _context;
        private readonly IHubContext<QueueHub> _hubContext;

        public QueueService(SmartClinicDbContext context, IHubContext<QueueHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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

        public async Task<bool> CallNextPatientAsync(string roomName)
        {
            // 1. Dọn dẹp: Chuyển người đang Calling (nếu có) sang "Examining"
            var currentCalling = await _context.QueueTickets
                .FirstOrDefaultAsync(t => t.Status == "Calling"); // Giả sử DB em có cột RoomName

            if (currentCalling != null)
            {
                currentCalling.Status = "Examining";
            }

            // 2. Tìm bệnh nhân tiếp theo đang chờ
            var nextPatient = await _context.QueueTickets
                .Where(t => t.Status == "Waiting")
                .OrderBy(t => t.TicketNumber)
                .FirstOrDefaultAsync();

            if (nextPatient == null)
                return false; // Hết bệnh nhân chờ

            // 3. Cập nhật trạng thái người mới thành "Calling"
            nextPatient.Status = "Calling";
            //nextPatient.RoomName = roomName; // Gán phòng cho bệnh nhân

            // LƯU DB: Phải lưu thành công thì mới bắn SignalR
            await _context.SaveChangesAsync();

            // 4. Lấy data mới nhất sau khi DB thay đổi
            var displayData = await GetDisplayDataAsync();

            // 5. Bắn SignalR ra TẤT CẢ các màn hình sảnh chờ
            await _hubContext.Clients.All.SendAsync("ReceiveNewCall", displayData);

            return true;
        }
    }
}
