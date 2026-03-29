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
        private const int WalkInOverflowCapacity = 3;

        private readonly IDbContextFactory<SmartClinicDbContext> _dbContextFactory;
        private readonly IQueueService _queueService;
        private readonly IHubContext<QueueHub> _hubContext;
        private readonly IHubContext<PatientHub> _patientHubContext;

        private static readonly TicketStatus[] WalkInQueueStatuses =
        {
            TicketStatus.Waiting,
            TicketStatus.Emergency,
            TicketStatus.Calling
        };

        public TicketService(IDbContextFactory<SmartClinicDbContext> dbContextFactory, IQueueService queueService, IHubContext<QueueHub> hubContext, IHubContext<PatientHub> patientHubContext)
        {
            _dbContextFactory = dbContextFactory;
            _queueService = queueService;
            _hubContext = hubContext;
            _patientHubContext = patientHubContext;
        }

        public async Task<User?> FindPatientByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var normalizedPhone = phone.Trim();

            using var _context = await _dbContextFactory.CreateDbContextAsync();
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.PhoneNumber == normalizedPhone &&
                    (u.RoleMask & PatientRoleMask) == PatientRoleMask);
        }

        public async Task<QueueTicket> GenerateTicketAsync(GenerateTicketRequest request)
        {
            return await GenerateTicketInternalAsync(request);
        }

        public async Task<QueueTicket> GenerateTicketAsync(string patientName, string? patientPhone, int departmentId, int? userId = null)
        {
            var request = new GenerateTicketRequest
            {
                PatientName = patientName,
                PatientPhone = patientPhone,
                DepartmentId = departmentId,
                UserId = userId,
                PatientGender = true,
                DoctorShiftId = null,
                IsEmergency = false,
                IsPriority = false
            };

            return await GenerateTicketInternalAsync(request);
        }

        public async Task<List<ReceptionRoomLiveItemDto>> GetAvailableShiftsAsync(int departmentId)
        {
            if (departmentId <= 0)
            {
                return new List<ReceptionRoomLiveItemDto>();
            }

            var now = DateTime.Now;
            var today = now.Date;
            var nowTime = now.TimeOfDay;

            using var _context = await _dbContextFactory.CreateDbContextAsync();
            var shifts = await _context.DoctorShifts
                .AsNoTracking()
                .Include(s => s.Doctor)
                .Include(s => s.Room)
                .Include(s => s.ShiftDefinition)
                .Where(s =>
                    s.Room.DepartmentId == departmentId &&
                    (s.Room.Flags & RoomFlags.IsActive) != 0 &&
                    s.Date == today &&
                    s.ShiftDefinition.EndTime > nowTime)
                .ToListAsync();

            if (shifts.Count == 0)
            {
                return new List<ReceptionRoomLiveItemDto>();
            }

            var shiftIds = shifts.Select(s => s.Id).Distinct().ToList();

            // Đếm ticket chiếm capacity theo DoctorShiftId (Appointment + Waiting + Emergency + Calling + Examinating)
            var occupiedStatuses = new[] {
                TicketStatus.Appointment, TicketStatus.Waiting, TicketStatus.Emergency,
                TicketStatus.Calling, TicketStatus.Examinating
            };
            var ticketCountsByShift = await _context.QueueTickets
                .AsNoTracking()
                .Where(t =>
                    t.DoctorShiftId != null &&
                    shiftIds.Contains(t.DoctorShiftId.Value) &&
                    occupiedStatuses.Contains(t.StatusEnum) &&
                    t.CreatedAt.Date == today)
                .GroupBy(t => new { t.DoctorShiftId, t.StatusEnum })
                .Select(g => new { g.Key.DoctorShiftId, g.Key.StatusEnum, Count = g.Count() })
                .ToListAsync();

            var available = shifts
                .Select(shift =>
                {
                    var shiftTickets = ticketCountsByShift.Where(x => x.DoctorShiftId == shift.Id).ToList();
                    var totalOccupied = shiftTickets.Sum(x => x.Count);
                    var waitingCount = shiftTickets
                        .Where(x => x.StatusEnum == TicketStatus.Waiting || x.StatusEnum == TicketStatus.Emergency
                            || x.StatusEnum == TicketStatus.Calling)
                        .Sum(x => x.Count);
                    var appointmentCount = shiftTickets
                        .Where(x => x.StatusEnum == TicketStatus.Appointment)
                        .Sum(x => x.Count);
                    var realRemain = Math.Max(0, shift.Capacity - totalOccupied);

                    return new ReceptionRoomLiveItemDto
                    {
                        DoctorShiftId = shift.Id,
                        RoomId = shift.RoomId,
                        RoomName = shift.Room?.Name ?? $"Phòng {shift.RoomId}",
                        DoctorName = shift.Doctor?.FullName ?? shift.Doctor?.Username ?? "Bác sĩ",
                        ShiftName = shift.ShiftDefinition.Name,
                        ShiftStartTime = shift.ShiftDefinition.StartTime,
                        ShiftEndTime = shift.ShiftDefinition.EndTime,
                        Capacity = shift.Capacity,
                        RemainCapacity = realRemain,
                        WaitingCount = waitingCount,
                        AppointmentCount = appointmentCount,
                        IsActiveNow = true
                    };
                })
                .OrderBy(r => r.WaitingCount)
                .ThenByDescending(r => r.RemainCapacity)
                .ThenBy(r => r.ShiftStartTime)
                .ThenBy(r => r.RoomName)
                .ToList();

            return MarkLeastBusyRooms(available);
        }

        public async Task<ReceptionDashboardDto> GetReceptionDashboardAsync(string? keyword = null)
        {
            var today = DateTime.Today;
            var nowTime = DateTime.Now.TimeOfDay;

            var appointmentItems = await GetTodayAppointmentItemsAsync(today, nowTime);
            appointmentItems = ApplyAppointmentKeywordFilter(appointmentItems, keyword);
            appointmentItems = SortAppointmentItems(appointmentItems);

            var waitingCountByRoom = await GetWaitingCountByRoomAsync(today);
            var liveRooms = await GetTodayLiveRoomsAsync(today, nowTime, waitingCountByRoom);
            liveRooms = MarkLeastBusyRooms(liveRooms);

            return new ReceptionDashboardDto
            {
                AppointmentTickets = appointmentItems,
                LiveRooms = liveRooms
            };
        }

        private async Task<List<ReceptionAppointmentItemDto>> GetTodayAppointmentItemsAsync(DateTime today, TimeSpan nowTime)
        {
            using var _context = await _dbContextFactory.CreateDbContextAsync();
            var appointmentTickets = await _context.QueueTickets
                .AsNoTracking()
                .Include(t => t.PatientUser)
                .Include(t => t.DoctorShift)
                    .ThenInclude(s => s!.ShiftDefinition)
                .Include(t => t.DoctorShift)
                    .ThenInclude(s => s!.Doctor)
                .Include(t => t.Room)
                .Where(t => t.DoctorShiftId != null
                            && t.DoctorShift != null
                            && t.DoctorShift.Date.Date == today
                            && (t.StatusEnum == TicketStatus.Appointment || t.StatusEnum == TicketStatus.Waiting))
                .ToListAsync();

            return appointmentTickets
                .Select(ticket =>
                {
                    var shiftEndTime = ticket.DoctorShift!.ShiftDefinition.EndTime;
                    var isExpired = ticket.StatusEnum == TicketStatus.Appointment && nowTime > shiftEndTime;

                    return new ReceptionAppointmentItemDto
                    {
                        TicketId = ticket.Id,
                        BookingCode = $"APT-{ticket.Id:D6}",
                        TicketNumber = ticket.TicketNumber,
                        AppointmentDate = ticket.DoctorShift.Date,
                        ShiftStartTime = ticket.DoctorShift.ShiftDefinition.StartTime,
                        ShiftEndTime = shiftEndTime,
                        ShiftName = ticket.DoctorShift.ShiftDefinition.Name,
                        PatientName = ticket.PatientUser?.FullName ?? "Chưa cập nhật",
                        PatientPhone = ticket.PatientUser?.PhoneNumber ?? string.Empty,
                        DoctorName = ticket.DoctorShift.Doctor?.FullName ?? ticket.DoctorShift.Doctor?.Username ?? "Bác sĩ",
                        RoomName = ticket.Room?.Name ?? $"Phòng {ticket.RoomId}",
                        Status = ticket.StatusEnum,
                        IsExpiredInShift = isExpired,
                        CanCheckIn = ticket.StatusEnum == TicketStatus.Appointment && !isExpired
                    };
                })
                .ToList();
        }

        private static List<ReceptionAppointmentItemDto> ApplyAppointmentKeywordFilter(
            List<ReceptionAppointmentItemDto> appointmentItems,
            string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return appointmentItems;

            var rawKeyword = keyword.Trim();
            var normalizedKeyword = rawKeyword.ToLowerInvariant();

            return appointmentItems
                .Where(item =>
                    item.BookingCode.ToLowerInvariant().Contains(normalizedKeyword)
                    || item.PatientName.ToLowerInvariant().Contains(normalizedKeyword)
                    || item.PatientPhone.Contains(rawKeyword, StringComparison.OrdinalIgnoreCase)
                    || item.RoomName.ToLowerInvariant().Contains(normalizedKeyword)
                    || item.DoctorName.ToLowerInvariant().Contains(normalizedKeyword))
                .ToList();
        }

        private static List<ReceptionAppointmentItemDto> SortAppointmentItems(List<ReceptionAppointmentItemDto> appointmentItems)
        {
            return appointmentItems
                .OrderBy(item => item.ShiftStartTime)
                .ThenBy(item => item.IsExpiredInShift ? 1 : 0)
                .ThenBy(item => item.CanCheckIn ? 0 : 1)
                .ThenBy(item => item.TicketNumber == 0 ? int.MaxValue : item.TicketNumber)
                .ThenBy(item => item.PatientName)
                .ToList();
        }

        private async Task<Dictionary<int, int>> GetWaitingCountByRoomAsync(DateTime today)
        {
            using var _context = await _dbContextFactory.CreateDbContextAsync();
            return await _context.QueueTickets
                .AsNoTracking()
                .Where(t => t.StatusEnum == TicketStatus.Waiting && t.CreatedAt.Date == today)
                .GroupBy(t => t.RoomId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
        }

        private async Task<List<ReceptionRoomLiveItemDto>> GetTodayLiveRoomsAsync(
            DateTime today,
            TimeSpan nowTime,
            IReadOnlyDictionary<int, int> waitingCountByRoom)
        {
            using var _context = await _dbContextFactory.CreateDbContextAsync();
            var todayShifts = await _context.DoctorShifts
                .AsNoTracking()
                .Include(s => s.Doctor)
                .Include(s => s.Room)
                .Include(s => s.ShiftDefinition)
                .Where(s => s.Date.Date == today)
                .ToListAsync();

            return todayShifts
                .Select(shift =>
                {
                    var waitingCount = waitingCountByRoom.TryGetValue(shift.RoomId, out var roomWaitingCount)
                        ? roomWaitingCount
                        : 0;

                    var isActiveNow = nowTime >= shift.ShiftDefinition.StartTime && nowTime <= shift.ShiftDefinition.EndTime;

                    return new ReceptionRoomLiveItemDto
                    {
                        DoctorShiftId = shift.Id,
                        RoomId = shift.RoomId,
                        RoomName = shift.Room?.Name ?? $"Phòng {shift.RoomId}",
                        DoctorName = shift.Doctor?.FullName ?? shift.Doctor?.Username ?? "Bác sĩ",
                        ShiftName = shift.ShiftDefinition.Name,
                        ShiftStartTime = shift.ShiftDefinition.StartTime,
                        ShiftEndTime = shift.ShiftDefinition.EndTime,
                        Capacity = shift.Capacity,
                        RemainCapacity = shift.RemainCapacity,
                        WaitingCount = waitingCount,
                        IsActiveNow = isActiveNow
                    };
                })
                .OrderByDescending(r => r.IsActiveNow)
                .ThenBy(r => r.WaitingCount)
                .ThenBy(r => r.ShiftStartTime)
                .ThenBy(r => r.RoomName)
                .ToList();
        }

        private static List<ReceptionRoomLiveItemDto> MarkLeastBusyRooms(List<ReceptionRoomLiveItemDto> liveRooms)
        {
            if (liveRooms.Count == 0)
                return liveRooms;

            var activeRooms = liveRooms.Where(r => r.IsActiveNow).ToList();
            var targetRooms = activeRooms.Count > 0 ? activeRooms : liveRooms;
            var leastBusyCount = targetRooms.Min(r => r.WaitingCount);

            foreach (var room in liveRooms)
            {
                room.IsLeastBusy = room.WaitingCount == leastBusyCount && (activeRooms.Count == 0 || room.IsActiveNow);
            }

            return liveRooms;
        }

        public async Task<AppointmentCheckInResultDto> ConfirmAppointmentCheckInAsync(int ticketId, int? receptionistUserId = null)
        {
            using var _context = await _dbContextFactory.CreateDbContextAsync();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT \"Id\" FROM \"QueueTickets\" WHERE \"Id\" = {0} FOR UPDATE",
                    ticketId);

                var ticket = await _context.QueueTickets
                    .Include(t => t.PatientUser)
                    .Include(t => t.DoctorShift)
                        .ThenInclude(s => s!.ShiftDefinition)
                    .Include(t => t.DoctorShift)
                        .ThenInclude(s => s!.Room)
                    .FirstOrDefaultAsync(t => t.Id == ticketId);

                if (ticket == null)
                    throw new BusinessException("Không tìm thấy lịch hẹn.");

                if (ticket.StatusEnum != TicketStatus.Appointment)
                    throw new BusinessException("Lịch hẹn này đã được check-in hoặc không còn ở trạng thái Appointment.");

                if (ticket.DoctorShift == null || ticket.DoctorShift.ShiftDefinition == null)
                    throw new BusinessException("Lịch hẹn thiếu thông tin ca khám. Vui lòng kiểm tra lại dữ liệu.");

                var now = DateTime.Now;
                var nowTime = now.TimeOfDay;
                var shift = ticket.DoctorShift.ShiftDefinition;

                if (ticket.DoctorShift.Date.Date != now.Date)
                    throw new BusinessException("Lịch hẹn này không thuộc ngày hôm nay.");

                if (nowTime > shift.EndTime)
                    throw new BusinessException("Ca khám này đã kết thúc. Vui lòng chuyển bệnh nhân sang ca hiện tại của bác sĩ.");

                var newStt = await _context.Database
                    .SqlQueryRaw<int>(@"SELECT nextval('""TicketNumberSeq""') AS ""Value""")
                    .SingleAsync();

                ticket.TicketNumber = newStt;
                ticket.StatusEnum = TicketStatus.Waiting;
                ticket.RoomId = ticket.DoctorShift.RoomId;
                ticket.DoctorId = ticket.DoctorShift.DoctorId;
                ticket.CreatedAt = now;
                ticket.UpdatedAt = now;
                ticket.UpdatedBy = receptionistUserId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                try
                {
                    var displayData = await _queueService.GetDisplayDataAsync(ticket.RoomId);
                    var groupName = $"Room_{ticket.RoomId}";

                    await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNewCall", displayData);
                    await _patientHubContext.Clients.Group(groupName).SendAsync("QueueTicketUpdated", new
                    {
                        ticketId = ticket.Id,
                        patientName = ticket.PatientUser?.FullName ?? "Bệnh nhân",
                        roomId = ticket.RoomId
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi gửi SignalR sau check-in lịch hẹn: {ex.Message}");
                }

                return new AppointmentCheckInResultDto
                {
                    TicketId = ticket.Id,
                    NewStt = newStt,
                    PatientName = ticket.PatientUser?.FullName ?? "Bệnh nhân",
                    RoomName = ticket.DoctorShift.Room?.Name ?? $"Phòng {ticket.RoomId}"
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<QueueTicket> GenerateTicketInternalAsync(GenerateTicketRequest request)
        {
            ValidateGenerateTicketRequest(request);

            var normalizedName = request.PatientName.Trim();

            var normalizedPhone = string.IsNullOrWhiteSpace(request.PatientPhone)
                ? null
                : request.PatientPhone.Trim();

            var now = DateTime.Now;
            var todayDate = now.Date;
            var currentTime = now.TimeOfDay;

            using var _context = await _dbContextFactory.CreateDbContextAsync();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var patient = await GetOrCreatePatientAsync(_context, normalizedName, normalizedPhone, request.PatientGender);

                var selectedShiftId = request.DoctorShiftId ?? await ResolveLeastBusyShiftIdAsync(_context, request.DepartmentId, todayDate, currentTime);
                var selectedShift = await LockAndLoadDoctorShiftAsync(_context, selectedShiftId);

                if ((selectedShift.Room.Flags & RoomFlags.IsActive) == 0 || selectedShift.Room.DepartmentId != request.DepartmentId)
                {
                    throw new BusinessException("Bác sĩ/ca trực không thuộc chuyên khoa đã chọn hoặc phòng không còn hoạt động.");
                }

                await ReserveShiftSlotOrThrowAsync(_context, selectedShift, todayDate, currentTime, request.IsEmergency);

                var nextTicketNumber = await _context.Database
                    .SqlQueryRaw<int>(@"SELECT nextval('""TicketNumberSeq""') AS ""Value""")
                    .SingleAsync();

                var (ticketStatus, ticketUpdatedAt) = await ResolveTicketStatusAndUpdatedAtAsync(
                    _context,
                    now,
                    todayDate,
                    selectedShift.RoomId,
                    request.IsEmergency,
                    request.IsPriority);

                var ticket = new QueueTicket
                {
                    PatientId = patient.Id,
                    TicketNumber = nextTicketNumber,
                    StatusEnum = ticketStatus,
                    RoomId = selectedShift.RoomId,
                    DoctorId = selectedShift.DoctorId,
                    DoctorShiftId = selectedShift.Id,
                    CreatedAt = now,
                    UpdatedAt = ticketUpdatedAt,
                    CreatedBy = request.UserId
                };

                _context.QueueTickets.Add(ticket);
                await _context.SaveChangesAsync();

                // Gán gán Navigation Property SAU KHI save để tránh EF Core tracking object graph phức tạp (AsNoTracking)
                ticket.Room = selectedShift.Room;

                await transaction.CommitAsync();

                try
                {
                    var displayData = await _queueService.GetDisplayDataAsync(selectedShift.RoomId);
                    var groupName = $"Room_{selectedShift.RoomId}";

                    await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNewCall", displayData);
                    await _patientHubContext.Clients.Group(groupName).SendAsync("QueueTicketUpdated", new
                    {
                        ticketId = ticket.Id,
                        patientName = normalizedName,
                        roomId = selectedShift.RoomId
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi gửi SignalR: {ex.Message}");
                }

                return ticket;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static void ValidateGenerateTicketRequest(GenerateTicketRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PatientName))
                throw new BusinessException("Vui lòng nhập tên bệnh nhân.");

            if (request.DepartmentId <= 0)
                throw new BusinessException("Vui lòng chọn chuyên khoa.");
        }

        private static int GetEffectiveWalkInRemainCapacity(int remainCapacity, int waitingCount)
        {
            if (remainCapacity > 0)
            {
                return remainCapacity;
            }

            return Math.Max(0, WalkInOverflowCapacity - waitingCount);
        }

        private static async Task<int> GetWalkInWaitingCountAsync(SmartClinicDbContext context, int doctorShiftId, DateTime todayDate)
        {
            return await context.QueueTickets
                .AsNoTracking()
                .Where(t =>
                    t.DoctorShiftId == doctorShiftId &&
                    WalkInQueueStatuses.Contains(t.StatusEnum) &&
                    t.CreatedAt.Date == todayDate)
                .CountAsync();
        }

        private static async Task ReserveShiftSlotOrThrowAsync(
            SmartClinicDbContext context,
            DoctorShift selectedShift,
            DateTime todayDate,
            TimeSpan currentTime,
            bool isEmergency)
        {
            if (isEmergency)
            {
                if (selectedShift.RemainCapacity > 0)
                {
                    selectedShift.RemainCapacity -= 1;
                }

                return;
            }

            if (!IsShiftActive(selectedShift, todayDate, currentTime))
                throw new BusinessException("Ca trực đã hết hiệu lực tại thời điểm hiện tại. Vui lòng chọn bác sĩ khác.");

            if (selectedShift.RemainCapacity > 0)
            {
                selectedShift.RemainCapacity -= 1;
                return;
            }

            var waitingCount = await GetWalkInWaitingCountAsync(context, selectedShift.Id, todayDate);
            var walkInRemain = GetEffectiveWalkInRemainCapacity(selectedShift.RemainCapacity, waitingCount);

            if (walkInRemain <= 0)
            {
                throw new BusinessException("Bác sĩ/Phòng này đã đầy hàng chờ tại quầy. Vui lòng chọn bác sĩ khác hoặc thử lại sau.");
            }
        }

        private static async Task<(TicketStatus Status, DateTime UpdatedAt)> ResolveTicketStatusAndUpdatedAtAsync(
            SmartClinicDbContext context,
            DateTime now,
            DateTime todayDate,
            int roomId,
            bool isEmergency,
            bool isPriority)
        {
            var status = isEmergency ? TicketStatus.Emergency : TicketStatus.Waiting;

            if (!isPriority || isEmergency)
            {
                return (status, now);
            }

            var nextWaitings = await context.QueueTickets
                .AsNoTracking()
                .Where(t =>
                    t.RoomId == roomId &&
                    (t.StatusEnum == TicketStatus.Waiting || t.StatusEnum == TicketStatus.Emergency) &&
                    t.CreatedAt.Date == todayDate)
                .OrderBy(t => t.StatusEnum == TicketStatus.Emergency ? 0 : 1)
                .ThenBy(t => t.UpdatedAt ?? t.CreatedAt)
                .ThenBy(t => t.TicketNumber)
                .Take(3)
                .ToListAsync();

            if (nextWaitings.Count == 0)
            {
                return (status, now);
            }

            var anchor = nextWaitings.Count < 3 ? nextWaitings[^1] : nextWaitings[2];
            var updatedAt = (anchor.UpdatedAt ?? anchor.CreatedAt).AddMilliseconds(1);
            return (status, updatedAt);
        }

        private static async Task<User> GetOrCreatePatientAsync(SmartClinicDbContext context, string patientName, string? patientPhone, bool patientGender)
        {
            if (!string.IsNullOrWhiteSpace(patientPhone))
            {
                var existingPatient = await context.Users.FirstOrDefaultAsync(u =>
                    u.PhoneNumber == patientPhone &&
                    (u.RoleMask & PatientRoleMask) == PatientRoleMask);

                if (existingPatient != null)
                {
                    return existingPatient;
                }
            }

            var walkInPatient = new User
            {
                Username = $"walkin_{Guid.NewGuid():N}",
                // PasswordHash hiện vẫn là cột bắt buộc trong schema User.
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                FullName = patientName,
                PhoneNumber = patientPhone,
                Gender = patientGender,
                RoleMask = PatientRoleMask,
                IsActive = true
            };
            context.Users.Add(walkInPatient);
            await context.SaveChangesAsync();

            return walkInPatient;
        }

        private static async Task<int> ResolveLeastBusyShiftIdAsync(SmartClinicDbContext context, int departmentId, DateTime todayDate, TimeSpan currentTime)
        {
            var activeShifts = await context.DoctorShifts
                .AsNoTracking()
                .Where(s =>
                    s.Room.DepartmentId == departmentId &&
                    (s.Room.Flags & RoomFlags.IsActive) != 0 &&
                    s.Date == todayDate &&
                    s.ShiftDefinition.EndTime > currentTime)
                .Select(s => new
                {
                    s.Id,
                    s.RoomId,
                    s.ShiftDefinition.StartTime,
                    s.RemainCapacity
                })
                .ToListAsync();

            if (activeShifts.Count == 0)
            {
                throw new BusinessException("Hiện tại không có phòng nào mở cửa cho khoa này!");
            }

            var shiftIds = activeShifts.Select(s => s.Id).Distinct().ToList();
            var waitingCountByShift = await context.QueueTickets
                .AsNoTracking()
                .Where(t =>
                    t.DoctorShiftId != null &&
                    shiftIds.Contains(t.DoctorShiftId.Value) &&
                    WalkInQueueStatuses.Contains(t.StatusEnum) &&
                    t.CreatedAt.Date == todayDate)
                .GroupBy(t => t.DoctorShiftId!.Value)
                .Select(g => new { ShiftId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ShiftId, x => x.Count);

            var selectableShifts = activeShifts
                .Where(s =>
                {
                    var waitingCount = waitingCountByShift.TryGetValue(s.Id, out var count) ? count : 0;
                    return GetEffectiveWalkInRemainCapacity(s.RemainCapacity, waitingCount) > 0;
                })
                .ToList();

            if (selectableShifts.Count == 0)
            {
                throw new BusinessException("Hiện tại các ca trực đã đầy hàng chờ tại quầy. Vui lòng thử lại sau!");
            }

            var roomIds = selectableShifts.Select(s => s.RoomId).Distinct().ToList();
            var waitingCountByRoom = await context.QueueTickets
                .AsNoTracking()
                .Where(t =>
                    roomIds.Contains(t.RoomId) &&
                    t.StatusEnum == TicketStatus.Waiting &&
                    t.CreatedAt.Date == todayDate)
                .GroupBy(t => t.RoomId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var roomCandidates = selectableShifts
                .GroupBy(s => s.RoomId)
                .Select(group =>
                {
                    var waitingCount = waitingCountByRoom.TryGetValue(group.Key, out var count) ? count : 0;
                    var targetShift = group.OrderBy(s => s.StartTime).First();
                    return new
                    {
                        ShiftId = targetShift.Id,
                        WaitingCount = waitingCount
                    };
                })
                .ToList();

            var minimumWaiting = roomCandidates.Min(x => x.WaitingCount);
            var leastBusyRooms = roomCandidates
                .Where(x => x.WaitingCount == minimumWaiting)
                .ToList();

            return leastBusyRooms[Random.Shared.Next(leastBusyRooms.Count)].ShiftId;
        }

        private static async Task<DoctorShift> LockAndLoadDoctorShiftAsync(SmartClinicDbContext context, int doctorShiftId)
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT \"Id\" FROM \"DoctorShifts\" WHERE \"Id\" = {0} FOR UPDATE",
                doctorShiftId);

            var shift = await context.DoctorShifts
                .Include(s => s.Room)
                .Include(s => s.ShiftDefinition)
                .FirstOrDefaultAsync(s => s.Id == doctorShiftId);

            if (shift == null)
            {
                throw new BusinessException("Không tìm thấy ca trực đã chọn.");
            }

            return shift;
        }

        private static bool IsShiftActive(DoctorShift shift, DateTime todayDate, TimeSpan currentTime)
        {
            return shift.Date.Date == todayDate &&
                   shift.ShiftDefinition.EndTime > currentTime;
        }
    }
}
