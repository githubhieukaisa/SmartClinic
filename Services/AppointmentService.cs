using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
using SmartClinic.Models;

namespace SmartClinic.Services;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// AppointmentService — Xử lý nghiệp vụ đặt lịch khám cho bệnh nhân.
///
/// Business Rules:
///   1. Mỗi bệnh nhân chỉ được có 1 lịch hẹn Active (Appointment) / ngày.
///   2. Nếu hủy, chỉ được đặt lại tối đa 2 lần nữa trong ngày (tổng max 3 booking, max 2 cancel).
///   3. Chỉ đặt được ca có RemainCapacity > 0.
///   4. Chỉ đặt ca trong tương lai (chưa bắt đầu).
///   5. Không đặt trùng ca đã đặt.
///   6. Hủy chỉ được hủy trước khi ca bắt đầu.
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public interface IAppointmentService
{
    Task<List<AppointmentShiftDto>> GetAvailableShiftsAsync(DateTime? dateFilter, int? doctorFilter, int? departmentFilter, string? searchDoctor);
    Task<List<DoctorFeedbackSummaryDto>> GetDoctorFeedbacksAsync(int doctorId);
    Task<(bool Success, string Message)> BookAppointmentAsync(int patientId, int doctorShiftId);
    Task<(bool Success, string Message)> CancelAppointmentAsync(int patientId, int ticketId);
    Task<List<MyAppointmentDto>> GetMyAppointmentsAsync(int patientId);
}

public class AppointmentService : IAppointmentService
{
    private readonly IDbContextFactory<SmartClinicDbContext> _factory;

    /// <summary>Số lần hủy tối đa trong ngày. Sau khi hủy >= 2 lần, không cho đặt tiếp.</summary>
    private const int MaxCancellationsPerDay = 2;

    public AppointmentService(IDbContextFactory<SmartClinicDbContext> factory)
    {
        _factory = factory;
    }

    // ══════════════════════════════════════════════════════════
    // 1. LẤY DANH SÁCH CA KHÁM CÒN CHỖ
    // ══════════════════════════════════════════════════════════
    public async Task<List<AppointmentShiftDto>> GetAvailableShiftsAsync(
        DateTime? dateFilter, int? doctorFilter, int? departmentFilter, string? searchDoctor)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var now = DateTime.Now;
        var today = now.Date;

        var query = ctx.DoctorShifts
            .AsNoTracking()
            .Include(s => s.Doctor)
            .Include(s => s.Room).ThenInclude(r => r.Department)
            .Include(s => s.ShiftDefinition)
            .Where(s => s.RemainCapacity > 0
                     && s.Date >= today
                     && s.StatusEnum == DoctorShiftStatus.Active);

        // Lọc theo ngày
        if (dateFilter.HasValue)
            query = query.Where(s => s.Date == dateFilter.Value.Date);

        // Lọc theo bác sĩ
        if (doctorFilter.HasValue && doctorFilter.Value > 0)
            query = query.Where(s => s.DoctorId == doctorFilter.Value);

        // Lọc theo khoa (Department)
        if (departmentFilter.HasValue && departmentFilter.Value > 0)
            query = query.Where(s => s.Room.DepartmentId == departmentFilter.Value);

        // Tìm kiếm theo tên bác sĩ
        if (!string.IsNullOrWhiteSpace(searchDoctor))
        {
            var search = searchDoctor.Trim().ToLower();
            query = query.Where(s => s.Doctor.FullName != null && s.Doctor.FullName.ToLower().Contains(search));
        }

        var shifts = await query
            .OrderBy(s => s.Date)
            .ThenBy(s => s.ShiftDefinition.SortOrder)
            .ToListAsync();

        // Lọc bỏ ca đã bắt đầu (in-memory vì ComputedStatus dùng DateTime.Now)
        var availableShifts = shifts
            .Where(s => s.ComputedStatus == "Sắp diễn ra")
            .ToList();

        // Lấy rating trung bình cho tất cả bác sĩ
        var doctorIds = availableShifts.Select(s => s.DoctorId).Distinct().ToList();
        var ratings = await ctx.DoctorEvaluations
            .AsNoTracking()
            .Where(e => e.DoctorId != null && doctorIds.Contains(e.DoctorId.Value) && e.IsSubmitted && e.Rating != null)
            .GroupBy(e => e.DoctorId)
            .Select(g => new { DoctorId = g.Key, AvgRating = g.Average(e => e.Rating!.Value), Count = g.Count() })
            .ToListAsync();

        var ratingMap = ratings.ToDictionary(r => r.DoctorId!.Value, r => (r.AvgRating, r.Count));

        return availableShifts.Select(s =>
        {
            ratingMap.TryGetValue(s.DoctorId, out var ratingInfo);
            return new AppointmentShiftDto
            {
                ShiftId = s.Id,
                DoctorId = s.DoctorId,
                DoctorName = s.Doctor.FullName ?? s.Doctor.Username,
                RoomName = s.Room.Name,
                DepartmentName = s.Room.Department.Name,
                RoomLocation = s.Room.Location,
                Date = s.Date,
                ShiftName = s.ShiftDefinition.Name,
                StartTime = s.ShiftDefinition.StartTime,
                EndTime = s.ShiftDefinition.EndTime,
                RemainCapacity = s.RemainCapacity,
                Capacity = s.Capacity,
                DoctorAvgRating = ratingInfo.AvgRating,
                DoctorReviewCount = ratingInfo.Count
            };
        }).ToList();
    }

    // ══════════════════════════════════════════════════════════
    // 2. LẤY FEEDBACK CỦA BÁC SĨ
    // ══════════════════════════════════════════════════════════
    public async Task<List<DoctorFeedbackSummaryDto>> GetDoctorFeedbacksAsync(int doctorId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        return await ctx.DoctorEvaluations
            .AsNoTracking()
            .Where(e => e.DoctorId == doctorId && e.IsSubmitted && e.Rating != null)
            .OrderByDescending(e => e.SubmittedAt)
            .Take(20)
            .Select(e => new DoctorFeedbackSummaryDto
            {
                Rating = e.Rating!.Value,
                Comment = e.Comment,
                SubmittedAt = e.SubmittedAt,
                PatientName = e.Patient != null ? e.Patient.FullName : null
            })
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════════════
    // 3. ĐẶT LỊCH KHÁM
    // ══════════════════════════════════════════════════════════
    public async Task<(bool Success, string Message)> BookAppointmentAsync(int patientId, int doctorShiftId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var today = DateTime.Today;

        // ── Validate: Đếm số lần hủy trong ngày ──
        var cancelledToday = await ctx.QueueTickets
            .CountAsync(t => t.PatientId == patientId
                          && t.CreatedAt.Date == today
                          && t.StatusEnum == TicketStatus.Cancelled
                          && t.DoctorShiftId != null);

        if (cancelledToday >= MaxCancellationsPerDay)
            return (false, $"Bạn đã hủy {cancelledToday} lịch hẹn trong hôm nay. Không thể đặt thêm để tránh lạm dụng hệ thống.");

        // ── Validate: Đã có lịch hẹn Active trong ngày? ──
        var hasActiveToday = await ctx.QueueTickets
            .AnyAsync(t => t.PatientId == patientId
                        && t.CreatedAt.Date == today
                        && t.DoctorShiftId != null
                        && t.StatusEnum == TicketStatus.Appointment);

        if (hasActiveToday)
            return (false, "Bạn đã có một lịch hẹn đang chờ trong hôm nay. Vui lòng hủy lịch cũ trước khi đặt mới.");

        // ── Load DoctorShift kèm ShiftDefinition ──
        var shift = await ctx.DoctorShifts
            .Include(s => s.ShiftDefinition)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.Id == doctorShiftId);

        if (shift == null)
            return (false, "Ca khám không tồn tại.");

        if (shift.RemainCapacity <= 0)
            return (false, "Ca khám đã hết chỗ. Vui lòng chọn ca khác.");

        // Không cho đặt ca đã bắt đầu/hoàn thành
        if (shift.ComputedStatus != "Sắp diễn ra")
            return (false, "Ca khám này đã bắt đầu hoặc đã kết thúc. Vui lòng chọn ca khác.");

        // Kiểm tra đã đặt trùng ca này chưa
        var alreadyBooked = await ctx.QueueTickets
            .AnyAsync(t => t.PatientId == patientId
                        && t.DoctorShiftId == doctorShiftId
                        && t.StatusEnum == TicketStatus.Appointment);
        if (alreadyBooked)
            return (false, "Bạn đã đặt lịch cho ca khám này rồi.");

        // ── Thực hiện đặt ──
        shift.RemainCapacity--;

        var ticket = new QueueTicket
        {
            TicketNumber = 0, // Sẽ được gán khi check-in tại quầy
            StatusEnum = TicketStatus.Appointment,
            DoctorId = shift.DoctorId,
            RoomId = shift.RoomId,
            DoctorShiftId = shift.Id,
            PatientId = patientId,
            CreatedBy = patientId
        };

        ctx.QueueTickets.Add(ticket);
        await ctx.SaveChangesAsync();

        return (true, $"Đặt lịch thành công! Ca {shift.ShiftDefinition.Name} ngày {shift.Date:dd/MM/yyyy}, phòng {shift.Room.Name}.");
    }

    // ══════════════════════════════════════════════════════════
    // 4. HỦY LỊCH HẸN
    // ══════════════════════════════════════════════════════════
    public async Task<(bool Success, string Message)> CancelAppointmentAsync(int patientId, int ticketId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var ticket = await ctx.QueueTickets
            .Include(t => t.DoctorShift).ThenInclude(s => s!.ShiftDefinition)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.PatientId == patientId);

        if (ticket == null)
            return (false, "Không tìm thấy lịch hẹn.");

        if (ticket.StatusEnum != TicketStatus.Appointment)
            return (false, "Chỉ có thể hủy lịch hẹn đang ở trạng thái 'Chờ khám'.");

        // Không cho hủy khi ca đã bắt đầu
        if (ticket.DoctorShift != null && ticket.DoctorShift.ComputedStatus != "Sắp diễn ra")
            return (false, "Không thể hủy lịch hẹn vì ca khám đã bắt đầu hoặc đã kết thúc.");

        // Trả lại chỗ
        if (ticket.DoctorShift != null)
            ticket.DoctorShift.RemainCapacity++;

        ticket.StatusEnum = TicketStatus.Cancelled;
        ticket.UpdatedAt = DateTime.Now;

        await ctx.SaveChangesAsync();

        return (true, "Đã hủy lịch hẹn thành công.");
    }

    // ══════════════════════════════════════════════════════════
    // 5. LỊCH HẸN CỦA TÔI
    // ══════════════════════════════════════════════════════════
    public async Task<List<MyAppointmentDto>> GetMyAppointmentsAsync(int patientId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        return await ctx.QueueTickets
            .AsNoTracking()
            .Include(t => t.Doctor)
            .Include(t => t.Room)
            .Include(t => t.DoctorShift).ThenInclude(s => s!.ShiftDefinition)
            .Where(t => t.PatientId == patientId && t.DoctorShiftId != null)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new MyAppointmentDto
            {
                TicketId = t.Id,
                DoctorName = t.Doctor != null ? t.Doctor.FullName ?? t.Doctor.Username : "—",
                RoomName = t.Room.Name,
                Date = t.DoctorShift != null ? t.DoctorShift.Date : t.CreatedAt.Date,
                ShiftName = t.DoctorShift != null ? t.DoctorShift.ShiftDefinition.Name : "—",
                StartTime = t.DoctorShift != null ? t.DoctorShift.ShiftDefinition.StartTime : TimeSpan.Zero,
                EndTime = t.DoctorShift != null ? t.DoctorShift.ShiftDefinition.EndTime : TimeSpan.Zero,
                Status = t.StatusEnum.ToString(),
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();
    }
}

// ══════════════════════════════════════════════════════════
// DTOs
// ══════════════════════════════════════════════════════════

public class AppointmentShiftDto
{
    public int ShiftId { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public string RoomLocation { get; set; } = "";
    public DateTime Date { get; set; }
    public string ShiftName { get; set; } = "";
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int RemainCapacity { get; set; }
    public int Capacity { get; set; }
    public double DoctorAvgRating { get; set; }
    public int DoctorReviewCount { get; set; }
}

public class DoctorFeedbackSummaryDto
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? PatientName { get; set; }
}

public class MyAppointmentDto
{
    public int TicketId { get; set; }
    public string DoctorName { get; set; } = "";
    public string RoomName { get; set; } = "";
    public DateTime Date { get; set; }
    public string ShiftName { get; set; } = "";
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
