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

        public async Task<Patient?> FindPatientByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;

            return await _context.Patients
                .FirstOrDefaultAsync(p => p.Phone == phone.Trim());
        }

        public async Task<QueueTicket> GenerateTicketAsync(string patientName, string patientPhone, int departmentId)
        {
            Patient patient = null;
            patientPhone = patientPhone?.Trim();

            if (!string.IsNullOrEmpty(patientPhone))
            {
                patient = await _context.Patients.FirstOrDefaultAsync(p => p.Phone == patientPhone);
                if (patient != null && patient.FullName != patientName)
                {
                    patient.FullName = patientName;
                    await _context.SaveChangesAsync();
                }
            }

            if (patient == null)
            {
                patient = new Patient
                {
                    FullName = patientName,
                    Phone = string.IsNullOrEmpty(patientPhone) ? null : patientPhone
                };
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync(); // Lưu luôn để có patient.Id
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.Database.ExecuteSqlRawAsync("SELECT \"Id\" FROM \"Departments\" WHERE \"Id\" = {0} FOR UPDATE", departmentId);

                var today = DateTime.UtcNow.Date;

                // 2. Tìm phòng trống nhất (Lúc này an toàn tuyệt đối, không sợ đọc trùng)
                var selectedRoomInfo = await _context.Rooms
                    .Where(r => r.DepartmentId == departmentId && r.IsActive)
                    .Select(r => new
                    {
                        Room = r,
                        WaitingCount = _context.QueueTickets.Count(t =>
                            t.RoomId == r.Id &&
                            t.Status == "Waiting" &&
                            t.CreatedAt >= today)
                    })
                    .OrderBy(x => x.WaitingCount)
                    .FirstOrDefaultAsync();

                if (selectedRoomInfo == null)
                {
                    throw new Exception("Hiện tại không có phòng nào mở cửa cho khoa này!");
                }

                // 3. Lấy số tự tăng (Độc lập, siêu nhanh)
                var nextTicketNumber = await _context.Database
                    .SqlQueryRaw<int>(@"SELECT nextval('""TicketNumberSeq""') AS ""Value""")
                    .SingleAsync();

                // 4. In vé
                var ticket = new QueueTicket
                {
                    PatientId = patient.Id,
                    TicketNumber = nextTicketNumber,
                    Status = "Waiting",
                    RoomId = selectedRoomInfo.Room.Id,
                    CreatedAt = DateTime.UtcNow,
                    Room = selectedRoomInfo.Room
                };

                _context.QueueTickets.Add(ticket);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                //Call SignalR
                try
                {
                    var displayData = await _queueService.GetDisplayDataAsync(selectedRoomInfo.Room.Id);
                    string groupName = $"Room_{selectedRoomInfo.Room.Id}";

                    // throw new Exception("BÙM! Đứt cáp quang biển, SignalR không gửi được tin nhắn!");

                    await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNewCall", displayData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi gửi SignalR: {ex.Message}");
                }

                return ticket;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

    }
}
