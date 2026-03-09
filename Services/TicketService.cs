using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class TicketService : ITicketService
    {
        private readonly SmartClinicDbContext _context;
        private readonly IQueueService _queueService;
        private readonly IHubContext<QueueHub> _hubContext;

        public TicketService(SmartClinicDbContext context, IQueueService queueService, IHubContext<QueueHub> hubContext)
        {
            _context = context;
            _queueService = queueService;
            _hubContext = hubContext;
        }

        public async Task<QueueTicket> GenerateTicketAsync(Patient newPatient, int departmentId)
        {
            _context.Patients.Add(newPatient);
            await _context.SaveChangesAsync();

            var selectedRoomInfo = await _context.Rooms
                .Where(r => r.DepartmentId == departmentId && r.IsActive)
                .Select(r => new
                {
                    Room = r,
                    WaitingCount = _context.QueueTickets.Count(t => t.Id == r.Id && t.Status == "Waiting")
                })
                .OrderBy(x => x.WaitingCount)
                .FirstOrDefaultAsync();

            if (selectedRoomInfo == null)
            {
                throw new Exception("Hiện tại không có phòng nào mở cửa cho khoa này!");
            }

            var nextTicketNumber = await _context.Database
                .SqlQueryRaw<int>(@"SELECT nextval('""TicketNumberSeq""') AS ""Value""")
                .SingleAsync();

            var ticket = new QueueTicket
            {
                PatientId = newPatient.Id,
                TicketNumber = nextTicketNumber,
                Status = "Waiting",
                RoomId = selectedRoomInfo.Room.Id,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            };

            _context.QueueTickets.Add(ticket);
            await _context.SaveChangesAsync();

            // 1. Lấy dữ liệu sảnh chờ mới nhất (lúc này đã có số vừa tạo nằm trong list Waiting)
            var displayData = await _queueService.GetDisplayDataAsync();

            // 2. Bắn SignalR tới TẤT CẢ các màn hình đang mở trang /waiting-room
            await _hubContext.Clients.All.SendAsync("ReceiveNewCall", displayData);

            return ticket;
        }
    }
}
