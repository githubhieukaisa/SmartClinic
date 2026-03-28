using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
using SmartClinic.DTOs;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class QueueService : IQueueService
    {
        private readonly IDbContextFactory<SmartClinicDbContext> _dbFactory;
        private readonly IHubContext<QueueHub> _hubContext;
        private const int _displayCount = 5;

        public QueueService(IDbContextFactory<SmartClinicDbContext> dbFactory, IHubContext<QueueHub> hubContext)
        {
            _dbFactory = dbFactory;
            _hubContext = hubContext;
        }

        public async Task<QueueDisplayDto> GetDisplayDataAsync(int roomId)
        {
            Console.WriteLine($"Fetching display data for RoomId {roomId}...");
            await using var context = await _dbFactory.CreateDbContextAsync();
            var today = DateTime.Today;

            // 1. TÌM CA TRỰC ĐANG HOẠT ĐỘNG CỦA PHÒNG NÀY
            var shiftsToday = await context.DoctorShifts
                .Include(s => s.Doctor)
                .Include(s => s.Room)
                .ThenInclude(r => r.Department)
                .Include(s => s.ShiftDefinition)
                .Where(s => s.RoomId == roomId && s.Date == today)
                .ToListAsync();

            var activeShift = shiftsToday.FirstOrDefault(s => s.ComputedStatus == "Đang trực");

            // 2. TÌM SỐ ĐANG GỌI
            var currentCall = await context.QueueTickets
                .Where(t => t.RoomId == roomId && t.StatusEnum == TicketStatus.Calling && t.CreatedAt.Date == today)
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .Select(t => new
                {
                    t.TicketNumber,
                    PatientName = t.PatientUser != null ? t.PatientUser.FullName : "N/A"
                })
                .FirstOrDefaultAsync();

            // 3. TÌM DANH SÁCH CHỜ (Emergency luôn đứng đầu)
            var nextTickets = await context.QueueTickets
                .Where(t => t.RoomId == roomId
                    && (t.StatusEnum == TicketStatus.Waiting || t.StatusEnum == TicketStatus.Emergency)
                    && t.CreatedAt.Date == today)
                .OrderBy(t => t.StatusEnum == TicketStatus.Emergency ? 0 : 1)
                .ThenBy(t => t.UpdatedAt ?? t.CreatedAt)
                .ThenBy(t => t.TicketNumber)
                .Take(_displayCount)
                .Select(t => t.TicketNumber.ToString())
                .ToListAsync();

            // 4. TRẢ VỀ CHO TIVI
            return new QueueDisplayDto
            {
                CurrentTicketNumber = currentCall?.TicketNumber.ToString() ?? "---",
                RoomName = activeShift?.Room.Name ?? $"Phòng {roomId}", // Nếu có ca trực thì lấy tên phòng từ DB
                DoctorName = activeShift != null ? $"BS. {activeShift.Doctor.FullName}" : "Phòng đang trống",
                PatientName = currentCall != null ? currentCall.PatientName : "Không có bệnh nhân",
                Specialty = activeShift?.Room.Department?.Name ?? "",
                NextTickets = nextTickets
            };
        }

        public async Task<bool> CallNextPatientAsync(int roomId)
        {
            var today = DateTime.Today;
            await using var context = await _dbFactory.CreateDbContextAsync();

            bool isExamining = await context.QueueTickets.AnyAsync(t => t.StatusEnum == TicketStatus.Examinating && t.CreatedAt.Date == today && t.RoomId == roomId);
            if (isExamining) return false;

            var currentCalling = await context.QueueTickets
                .FirstOrDefaultAsync(t => t.StatusEnum == TicketStatus.Calling && t.CreatedAt.Date == today && t.RoomId == roomId);
            if (currentCalling != null)
            {
                currentCalling.MissCount += 1;

                if (currentCalling.MissCount >= 5)
                {
                    currentCalling.StatusEnum = TicketStatus.Missed;

                    // Log
                    Console.WriteLine($"[System] Bệnh nhân {currentCalling.TicketNumber} đã vắng mặt 3 lần. Chuyển trạng thái thành Missed.");
                }
                else
                {
                    currentCalling.StatusEnum = TicketStatus.Waiting;
                    currentCalling.UpdatedAt = DateTime.Now;
                    // Tìm 3 người tiếp theo đang chờ
                    var nextWaitings = await context.QueueTickets
                        .Where(t => (t.StatusEnum == TicketStatus.Waiting || t.StatusEnum == TicketStatus.Emergency)
                            && t.CreatedAt.Date == today && t.RoomId == roomId)
                        .OrderBy(t => t.StatusEnum == TicketStatus.Emergency ? 0 : 1)
                        .ThenBy(t => t.UpdatedAt ?? t.CreatedAt)
                        .Take(3) // Số N = 3
                        .ToListAsync();

                    if (nextWaitings.Count == 0)
                    {
                        // Nếu đằng sau không còn ai, do nothing
                    }
                    else if (nextWaitings.Count < 3)
                    {
                        // Nếu đằng sau chỉ có 1 hoặc 2 người (ít hơn 3), thì đẩy ổng xuống CUỐI CÙNG
                        var lastPerson = nextWaitings[^1];
                        currentCalling.UpdatedAt = (lastPerson.UpdatedAt ?? lastPerson.CreatedAt).AddMilliseconds(1);
                    }
                    else
                    {
                        // Nếu đằng sau đông người, lấy người thứ 3 làm mốc, nhét vào ngay sau người thứ 3
                        var thirdPerson = nextWaitings[2];
                        currentCalling.UpdatedAt = (thirdPerson.UpdatedAt ?? thirdPerson.CreatedAt).AddMilliseconds(1);
                    }
                }
            }

            // 2. Tìm bệnh nhân tiếp theo đang chờ
            var nextPatient = await context.QueueTickets
                .Where(t => (t.StatusEnum == TicketStatus.Waiting || t.StatusEnum == TicketStatus.Emergency)
                    && t.CreatedAt.Date == today && t.RoomId == roomId)
                .OrderBy(t => t.StatusEnum == TicketStatus.Emergency ? 0 : 1)
                .ThenBy(t => t.UpdatedAt ?? t.CreatedAt)
                .ThenBy(t => t.TicketNumber)
                .FirstOrDefaultAsync();

            if (nextPatient == null) return false; // Hết bệnh nhân chờ

            // 3. Cập nhật trạng thái người mới thành "Calling"
            nextPatient.StatusEnum = TicketStatus.Calling;
            nextPatient.CreatedAt = today;
            nextPatient.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();

            // 4. Lấy data mới nhất sau khi DB thay đổi
            var displayData = await GetDisplayDataAsync(nextPatient.RoomId);

            string groupName = $"Room_{roomId}";
            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNewCall", displayData);

            return true;
        }
    }
}
