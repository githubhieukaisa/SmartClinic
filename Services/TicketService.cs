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
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Phone == patientPhone);

                if (patient == null)
                {
                    // Bệnh nhân mới -> Tạo mới
                    patient = new Patient { FullName = patientName, Phone = patientPhone };
                    _context.Patients.Add(patient);
                    await _context.SaveChangesAsync(); // Lưu để lấy ID mới
                }
                else
                {
                    // Bệnh nhân cũ -> Cập nhật tên nếu Lễ tân có sửa đổi
                    if (patient.FullName != patientName)
                    {
                        patient.FullName = patientName;
                        await _context.SaveChangesAsync();
                    }
                }

                // 2. Chạy Lock và Load Balancing như cũ
                await _context.Database.ExecuteSqlRawAsync("SELECT \"Id\" FROM \"Departments\" WHERE \"Id\" = {0} FOR UPDATE", departmentId);

                var today = DateTime.UtcNow.Date;
                // 3. Lúc này đã an toàn 100%
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

                // 4. Lấy số Sequence
                var nextTicketNumber = await _context.Database
                    .SqlQueryRaw<int>(@"SELECT nextval('""TicketNumberSeq""') AS ""Value""")
                    .SingleAsync();

                // 5. Tạo Ticket
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
