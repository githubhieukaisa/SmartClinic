using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
using SmartClinic.DTOs;
using SmartClinic.Hubs;
using SmartClinic.Models;
using SmartClinic.Services.Exceptions;

namespace SmartClinic.Services
{
    public class TicketService : ITicketService
    {
        private readonly SmartClinicDbContext _context;
        private readonly IQueueService _queueService;
        private readonly IHubContext<QueueHub> _hubContext;
        private readonly IHubContext<PatientHub> _patientHubContext;

        public TicketService(SmartClinicDbContext context, IQueueService queueService, IHubContext<QueueHub> hubContext, IHubContext<PatientHub> patientHubContext)
        {
            _context = context;
            _queueService = queueService;
            _hubContext = hubContext;
            _patientHubContext = patientHubContext;
        }

        public async Task<Patient?> FindPatientByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;

            return await _context.Patients
                .FirstOrDefaultAsync(p => p.Phone == phone.Trim());
        }

        public async Task<QueueTicket> GenerateTicketAsync(GenerateTicketRequest request)
        {
            return await GenerateTicketAsync(request.PatientName, request.PatientPhone, request.DepartmentId, request.UserId, request.PatientGender);
        }

        public async Task<QueueTicket> GenerateTicketAsync(string patientName, string patientPhone, int departmentId, int? userId = null)
        {
            return await GenerateTicketAsync(patientName, patientPhone, departmentId, userId, true);
        }

        private async Task<QueueTicket> GenerateTicketAsync(string patientName, string patientPhone, int departmentId, int? userId, bool patientGender)
        {
            Patient patient = null;
            patientPhone = patientPhone?.Trim();

            if (!string.IsNullOrEmpty(patientPhone))
            {
                patient = await _context.Patients.FirstOrDefaultAsync(p => p.Phone == patientPhone);
                if (patient != null && patient.FullName != patientName)
                {
                    patient.FullName = patientName;
                    patient.Gender = patientGender;
                    await _context.SaveChangesAsync();
                }
            }

            if (patient == null)
            {
                patient = new Patient
                {
                    FullName = patientName,
                    Phone = string.IsNullOrEmpty(patientPhone) ? null : patientPhone,
                    Gender = patientGender
                };
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync(); // Lưu luôn để có patient.Id
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.Database.ExecuteSqlRawAsync("SELECT \"Id\" FROM \"Departments\" WHERE \"Id\" = {0} FOR UPDATE", departmentId);

                var today = DateTime.UtcNow;

                var todayDate = DateTime.Today;
                var nowTime = DateTime.Now;

                // 2. Tìm phòng đang Active (Có ca trực hiện hành)
                var roomsWithShifts = await _context.Rooms
                    .Include(r => r.DoctorShifts)
                        .ThenInclude(ds => ds.ShiftDefinition)
                    .Where(r => r.DepartmentId == departmentId 
                        && (r.Flags & RoomFlags.IsActive) != 0
                        && r.DoctorShifts.Any(ds => ds.Date == todayDate))
                    .ToListAsync();
                    
                var activeRooms = roomsWithShifts
                    .Where(r => r.DoctorShifts.Any(ds => ds.Date == todayDate && ds.ComputedStatus == "Đang trực"))
                    .Select(r => new
                    {
                        Room = r,
                        WaitingCount = _context.QueueTickets.Count(t =>
                           t.RoomId == r.Id &&
                           t.StatusEnum == TicketStatus.Waiting &&
                           t.CreatedAt.Date == todayDate) // Lọc chờ theo ngày
                    })
                    .OrderBy(x => x.WaitingCount)
                    .FirstOrDefault();

                if (activeRooms == null)
                {
                    throw new BusinessException("Hiện tại không có phòng nào mở cửa cho khoa này!");
                }

                var selectedRoomInfo = activeRooms;

                // 3. Lấy số tự tăng
                var nextTicketNumber = await _context.Database
                    .SqlQueryRaw<int>(@"SELECT nextval('""TicketNumberSeq""') AS ""Value""")
                    .SingleAsync();

                // 4. In vé
                var ticket = new QueueTicket
                {
                    PatientId = patient.Id,
                    TicketNumber = nextTicketNumber,
                    StatusEnum = TicketStatus.Waiting,
                    RoomId = selectedRoomInfo.Room.Id,
                    CreatedAt = DateTime.UtcNow,
                    Room = selectedRoomInfo.Room,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
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
                    await _patientHubContext.Clients.Group(groupName).SendAsync("QueueTicketUpdated", new
                    {
                        ticketId = ticket.Id,
                        patientName,
                        roomId = selectedRoomInfo.Room.Id
                    });
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
