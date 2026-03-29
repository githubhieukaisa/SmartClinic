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

        private readonly IDbContextFactory<SmartClinicDbContext> _factory;
        private readonly IQueueService _queueService;
        private readonly IHubContext<QueueHub> _hubContext;
        private readonly IHubContext<PatientHub> _patientHubContext;

        public TicketService(IDbContextFactory<SmartClinicDbContext> factory, IQueueService queueService, IHubContext<QueueHub> hubContext, IHubContext<PatientHub> patientHubContext)
        {
            _factory = factory;
            _queueService = queueService;
            _hubContext = hubContext;
            _patientHubContext = patientHubContext;
        }

        public async Task<User?> FindPatientByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;

            await using var _context = await _factory.CreateDbContextAsync();
            var normalizedPhone = phone.Trim();

            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.PhoneNumber == normalizedPhone &&
                    (u.RoleMask & PatientRoleMask) == PatientRoleMask);
        }

        public async Task<QueueTicket> GenerateTicketAsync(GenerateTicketRequest request)
        {
            return await GenerateTicketAsync(request.PatientName, request.PatientPhone ?? string.Empty, request.DepartmentId, request.UserId, request.PatientGender, request.StatusEnum);
        }

        public async Task<QueueTicket> GenerateTicketAsync(string patientName, string patientPhone, int departmentId, int? userId = null)
        {
            return await GenerateTicketAsync(patientName, patientPhone, departmentId, userId, true, TicketStatus.Waiting);
        }

        private async Task<QueueTicket> GenerateTicketAsync(string patientName, string patientPhone, int departmentId, int? userId, bool patientGender, TicketStatus status = TicketStatus.Waiting)
        {
            await using var _context = await _factory.CreateDbContextAsync();
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

                var todayDate = DateTime.Today;
                var nowTime = DateTime.Now;

                // 2. Tìm phòng đang Active (Có ca trực hiện hành)
                var roomsWithShifts = await _context.Rooms
                    .AsNoTracking()
                    .Include(r => r.DoctorShifts)
                        .ThenInclude(ds => ds.ShiftDefinition)
                    .Where(r => r.DepartmentId == departmentId
                        && (r.Flags & RoomFlags.IsActive) != 0
                        && r.DoctorShifts.Any(ds => ds.Date == todayDate && ds.ComputedStatus == "Đang trong ca"))
                    .ToListAsync();

                var activeRooms = roomsWithShifts
                    .Select(r => new
                    {
                        Room = r,
                        // Quan trọng: Chỉ lấy ca có Status là Active và đang trong khung giờ trực
                        ActiveShift = r.DoctorShifts.FirstOrDefault(ds =>
                            ds.Date == todayDate &&
                            ds.StatusEnum == DoctorShiftStatus.Active &&
                            ds.ComputedStatus == "Đang trực"),
                        WaitingCount = _context.QueueTickets.Count(t =>
                           t.RoomId == r.Id &&
                           (t.StatusEnum == TicketStatus.Waiting || t.StatusEnum == TicketStatus.Emergency) &&
                           t.CreatedAt.Date == todayDate)
                    })
                    .Where(x => x.ActiveShift != null)
                    .OrderBy(x => x.WaitingCount)
                    .FirstOrDefault();

                if (activeRooms == null)
                {
                    throw new BusinessException("Hiện tại không có phòng nào mở cửa cho khoa này!");
                }

                // 2. Lấy Shift ID và thực hiện trừ Capacity một cách tường minh
                var selectedShiftId = activeRooms.ActiveShift!.Id;
                var shiftToUpdate = await _context.DoctorShifts.FindAsync(selectedShiftId);

                if (shiftToUpdate == null)
                {
                    throw new BusinessException("Không tìm thấy thông tin ca trực hợp lệ!");
                }

                if (shiftToUpdate.RemainCapacity <= 0)
                {
                    throw new BusinessException($"Ca trực của BS. {activeRooms.ActiveShift.Doctor.FullName} đã hết chỗ. Không thể tiếp nhận thêm bệnh nhân!");
                }

                shiftToUpdate.RemainCapacity--;

                // 3. Lấy số tự tăng
                var nextTicketNumber = await _context.Database
                    .SqlQueryRaw<int>(@"SELECT nextval('""TicketNumberSeq""') AS ""Value""")
                    .SingleAsync();

                // 4. In vé
                var ticket = new QueueTicket
                {
                    PatientId = patient.Id,
                    TicketNumber = nextTicketNumber,
                    StatusEnum = status,
                    AdditionalNotes = status == TicketStatus.Emergency ? "[EMERGENCY]" : null,
                    RoomId = activeRooms.Room.Id,
                    DoctorId = activeRooms.ActiveShift!.DoctorId,
                    DoctorShiftId = activeRooms.ActiveShift!.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                };

                _context.QueueTickets.Add(ticket);
                await _context.SaveChangesAsync();

                // Gán gán Navigation Property SAU KHI save để tránh EF Core tracking object graph phức tạp (AsNoTracking)
                ticket.Room = activeRooms.Room;

                await transaction.CommitAsync();

                //Call SignalR
                try
                {
                    var displayData = await _queueService.GetDisplayDataAsync(activeRooms.Room.Id);
                    string groupName = $"Room_{activeRooms.Room.Id}";

                    // throw new Exception("BÙM! Đứt cáp quang biển, SignalR không gửi được tin nhắn!");

                    await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNewCall", displayData);
                    await _patientHubContext.Clients.Group(groupName).SendAsync("QueueTicketUpdated", new
                    {
                        ticketId = ticket.Id,
                        patientName,
                        roomId = activeRooms.Room.Id
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
