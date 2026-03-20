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
        private const int _displayCount = 5;

        public QueueService(SmartClinicDbContext context, IHubContext<QueueHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<QueueDisplayDto> GetDisplayDataAsync(int roomId)
        {
            Console.WriteLine($"Fetching display data for RoomId {roomId}...");
            // 1. TÌM CA TRỰC ĐANG HOẠT ĐỘNG CỦA PHÒNG NÀY
            var activeShift = await _context.DoctorShifts
                .Where(s => s.RoomId == roomId && s.Status == "Active")
                .Select(s => new
                {
                    DoctorName = s.Doctor.FullName,
                    RoomName = s.Room.Name,
                    DepartmentName = s.Room.Department.Name
                })
                .FirstOrDefaultAsync();

            var today = DateTime.Today;

            // 2. TÌM SỐ ĐANG GỌI (Nhớ thêm điều kiện RoomId)
            var currentCall = await _context.QueueTickets
                .Where(t => t.RoomId == roomId && t.Status == "Calling" && t.CreatedAt.Date == today)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.TicketNumber,
                    PatientName = t.Patient.FullName
                })
                .FirstOrDefaultAsync();

            // 3. TÌM DANH SÁCH CHỜ (Nhớ thêm điều kiện RoomId)
            var nextTickets = await _context.QueueTickets
                .Where(t => t.RoomId == roomId && t.Status == "Waiting" && t.CreatedAt.Date == today)
                .OrderBy(t => t.CreatedAt)
                .ThenBy(t => t.TicketNumber)
                .Take(_displayCount)
                .Select(t => t.TicketNumber.ToString())
                .ToListAsync();

            // 4. TRẢ VỀ CHO TIVI
            return new QueueDisplayDto
            {
                CurrentTicketNumber = currentCall?.TicketNumber.ToString() ?? "---",
                RoomName = activeShift?.RoomName ?? $"Phòng {roomId}", // Nếu có ca trực thì lấy tên phòng từ DB
                DoctorName = activeShift != null ? $"BS. {activeShift.DoctorName}" : "Phòng đang trống",
                PatientName = currentCall != null ? currentCall.PatientName : "Không có bệnh nhân",
                Specialty = activeShift?.DepartmentName ?? "",
                NextTickets = nextTickets
            };
        }

        public async Task<bool> CallNextPatientAsync(int roomId)
        {
            var today = DateTime.Today;

            bool isExamining = await _context.QueueTickets.AnyAsync(t => t.Status == "Examining" && t.CreatedAt.Date == today && t.RoomId == roomId);
            if (isExamining) return false;

            var currentCalling = await _context.QueueTickets
                .FirstOrDefaultAsync(t => t.Status == "Calling" && t.CreatedAt.Date == today && t.RoomId == roomId);

            if (currentCalling != null)
            {
                currentCalling.MissCount += 1;

                if (currentCalling.MissCount >= 5)
                {
                    currentCalling.Status = "Missed";

                    // Log lại để IT/Admin biết
                    Console.WriteLine($"[System] Bệnh nhân {currentCalling.TicketNumber} đã vắng mặt 3 lần. Chuyển trạng thái thành Missed.");
                }
                else
                {
                    currentCalling.Status = "Waiting";
                    // Tìm 3 người tiếp theo đang chờ
                    var nextWaitings = await _context.QueueTickets
                        .Where(t => t.Status == "Waiting" && t.CreatedAt.Date == today && t.RoomId == roomId)
                        .OrderBy(t => t.CreatedAt)
                        .Take(3) // Số N = 3
                        .ToListAsync();

                    if (nextWaitings.Count == 0)
                    {
                        // Nếu đằng sau không còn ai, thì cứ để ổng ở nguyên vị trí (không đổi CreatedAt)
                    }
                    else if (nextWaitings.Count < 3)
                    {
                        // Nếu đằng sau chỉ có 1 hoặc 2 người (ít hơn 3), thì đẩy ổng xuống CUỐI CÙNG của nhóm ít ỏi đó
                        var lastPerson = nextWaitings[^1];
                        currentCalling.CreatedAt = lastPerson.CreatedAt.AddMilliseconds(1);
                    }
                    else
                    {
                        // Nếu đằng sau đông người, lấy người thứ 3 làm mốc, nhét ổng vào ngay sau người thứ 3
                        var thirdPerson = nextWaitings[2];
                        currentCalling.CreatedAt = thirdPerson.CreatedAt.AddMilliseconds(1);
                    }
                }
            }

            // 2. Tìm bệnh nhân tiếp theo đang chờ
            var nextPatient = await _context.QueueTickets
                .Where(t => t.Status == "Waiting" && t.CreatedAt.Date == today && t.RoomId == roomId)
                .OrderBy(t => t.CreatedAt)
                .ThenBy(t => t.TicketNumber)
                .FirstOrDefaultAsync();

            if (nextPatient == null) return false; // Hết bệnh nhân chờ

            // 3. Cập nhật trạng thái người mới thành "Calling"
            nextPatient.Status = "Calling";
            //nextPatient.RoomName = roomName; // Gán phòng cho bệnh nhân

            await _context.SaveChangesAsync();

            // 4. Lấy data mới nhất sau khi DB thay đổi
            var displayData = await GetDisplayDataAsync(nextPatient.RoomId);

            string groupName = $"Room_{roomId}";
            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNewCall", displayData);

            return true;
        }
    }
}
