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
        private const int PatientRoleMask = 128;

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

        public async Task<User?> FindPatientByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;

            var normalizedPhone = phone.Trim();

            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.PhoneNumber == normalizedPhone &&
                    (u.RoleMask & PatientRoleMask) == PatientRoleMask);
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
            User? patient = null;
            patientPhone = patientPhone?.Trim();

            if (!string.IsNullOrEmpty(patientPhone))
            {
                patient = await _context.Users.FirstOrDefaultAsync(u =>
                    u.PhoneNumber == patientPhone &&
                    (u.RoleMask & PatientRoleMask) == PatientRoleMask);

                if (patient != null && (patient.FullName != patientName || patient.Gender != patientGender))
                {
                    patient.FullName = patientName;
                    patient.Gender = patientGender;
                    await _context.SaveChangesAsync();
                }
            }

            if (patient == null)
            {
                patient = new User
                {
                    Username = $"patient_{Guid.NewGuid():N}",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                    FullName = patientName,
                    PhoneNumber = string.IsNullOrEmpty(patientPhone) ? null : patientPhone,
                    Gender = patientGender,
                    RoleMask = PatientRoleMask,
                    IsActive = true
                };
                _context.Users.Add(patient);
                await _context.SaveChangesAsync(); // Lưu luôn để có patient.Id
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.Database.ExecuteSqlRawAsync("SELECT \"Id\" FROM \"Departments\" WHERE \"Id\" = {0} FOR UPDATE", departmentId);

                var today = DateTime.UtcNow;

                // 2. Tìm phòng trống nhất
                var selectedRoomInfo = await _context.Rooms
                    .Where(r => r.DepartmentId == departmentId 
                        && (r.Flags & RoomFlags.IsActive) != 0
                        && r.DoctorShifts.Any(ds => ds.StartTime <= today && (ds.EndTime == null || ds.EndTime >= today)))
                    .Select(r => new
                    {
                        Room = r,
                        WaitingCount = _context.QueueTickets.Count(t =>
                            t.RoomId == r.Id &&
                            t.StatusEnum == TicketStatus.Waiting &&
                            t.CreatedAt >= today)
                    })
                    .OrderBy(x => x.WaitingCount)
                    .FirstOrDefaultAsync();

                if (selectedRoomInfo == null)
                {
                    throw new BusinessException("Hiện tại không có phòng nào mở cửa cho khoa này!");
                }

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
