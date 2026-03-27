using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
using SmartClinic.DTOs;
using SmartClinic.Models;

namespace SmartClinic.Services;

public interface IDoctorShiftService
{
    Task<List<DoctorShiftDisplayDto>> GetShiftsAsync(DateTime from, DateTime to);
    Task<List<User>> GetDoctorsAsync();
    Task<List<Room>> GetRoomsAsync();
    Task<(bool Success, string Error)> CreateShiftAsync(CreateShiftDto dto);
    Task<bool> DeleteShiftAsync(int id);
}

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// DoctorShiftService — Luồng hoạt động:
/// ═══════════════════════════════════════════════════════════════
///
/// ► Dùng IDbContextFactory thay vì inject DbContext trực tiếp.
///   Lý do: Blazor Server dùng long-lived circuit. DbContext được
///   resolved từ DI scope có thể bị dispose sau khi render xong,
///   dẫn đến ObjectDisposedException khi click event gọi service.
///   → Mỗi method tự tạo DbContext mới (using) → dùng xong tự dispose.
///   → Đây là pattern chuẩn của QueueService/PatientService/LabService
///      trong project này.
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class DoctorShiftService : IDoctorShiftService
{
    private readonly IDbContextFactory<SmartClinicDbContext> _factory;

    public DoctorShiftService(IDbContextFactory<SmartClinicDbContext> factory)
    {
        _factory = factory;
    }

    // ══════════════════════════════════════════════════════════
    // 1. LẤY DANH SÁCH CA TRỰC
    //    Join: DoctorShift → Doctor + Room → Department
    //    Filter: StartTime nằm trong khoảng [from, to]
    //    Sort: theo StartTime tăng dần
    // ══════════════════════════════════════════════════════════
    public async Task<List<DoctorShiftDisplayDto>> GetShiftsAsync(DateTime from, DateTime to)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var fromDate = from.Date;
        var toDate = to.Date.AddDays(1);

        return await ctx.DoctorShifts
            .AsNoTracking()
            .Include(s => s.Doctor)
            .Include(s => s.Room)
                .ThenInclude(r => r.Department)
            .Where(s => s.StartTime < toDate && (s.EndTime == null || s.EndTime >= fromDate))
            .OrderBy(s => s.StartTime)
            .Select(s => new DoctorShiftDisplayDto
            {
                Id = s.Id,
                DoctorId = s.DoctorId,
                DoctorName = s.Doctor.FullName ?? s.Doctor.Username,
                RoomId = s.RoomId,
                RoomName = s.Room.Name,
                DepartmentName = s.Room.Department.Name,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Status = s.StatusEnum == DoctorShiftStatus.Active ? "Đang trực" : "Đã kết thúc"
            })
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════════════
    // 2. LẤY DANH SÁCH BÁC SĨ
    //    Filter: (RoleMask & 2) == 2 → có role Bác sĩ
    //            IsActive == true
    // ══════════════════════════════════════════════════════════
    public async Task<List<User>> GetDoctorsAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        return await ctx.Users
            .AsNoTracking()
            .Where(u => (u.RoleMask & 2) == 2 && u.IsActive == true)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════════════
    // 3. LẤY DANH SÁCH PHÒNG KHÁM
    //    Filter: IsActive && !IsLab (dùng Flags bitwise)
    //    Sort: theo Khoa → tên phòng
    // ══════════════════════════════════════════════════════════
    public async Task<List<Room>> GetRoomsAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        return await ctx.Rooms
            .AsNoTracking()
            .Include(r => r.Department)
            .Where(r => (r.Flags & RoomFlags.IsActive) != 0
                     && (r.Flags & RoomFlags.IsLab) == 0)
            .OrderBy(r => r.Department.Name)
            .ThenBy(r => r.Name)
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════════════
    // 4. TẠO CA TRỰC MỚI (có validation chống trùng lịch)
    //
    //    Validate:
    //      a) EndTime > StartTime (nếu có EndTime)
    //      b) Doctor không có ca Active trùng giờ (overlap)
    //      c) Room không có ca Active trùng giờ (overlap)
    //
    //    Overlap formula:
    //      existingStart < newEnd  AND  existingEnd > newStart
    //      (nếu EndTime = null → coi như DateTime.MaxValue)
    //
    //    Fix sequence lệch: Doctor/Index.razor từng ép Id thủ công
    //    → PostgreSQL sequence bị lệch → duplicate key khi insert.
    //    → Dùng setval() để reset sequence trước khi insert.
    // ══════════════════════════════════════════════════════════
    public async Task<(bool Success, string Error)> CreateShiftAsync(CreateShiftDto dto)
    {
        // ── Validate: Không cho phân lịch trong quá khứ ──
        if (dto.StartTime < DateTime.Today)
            return (false, "Không thể phân lịch trực cho ngày trong quá khứ.");

        if (dto.EndTime.HasValue && dto.EndTime.Value <= dto.StartTime)
            return (false, "Thời gian kết thúc phải sau thời gian bắt đầu.");

        var newStart = dto.StartTime;
        var newEnd = dto.EndTime;

        await using var ctx = await _factory.CreateDbContextAsync();

        // ── Validate: Doctor không trùng lịch ──
        var doctorOverlap = await ctx.DoctorShifts
            .AnyAsync(s => s.DoctorId == dto.DoctorId
                        && s.StatusEnum == DoctorShiftStatus.Active
                        && s.StartTime < (newEnd ?? DateTime.MaxValue)
                        && (s.EndTime == null || s.EndTime > newStart));

        if (doctorOverlap)
            return (false, "Bác sĩ này đã có ca trực trùng thời gian. Vui lòng chọn khung giờ khác.");

        // ── Validate: Room không trùng lịch ──
        var roomOverlap = await ctx.DoctorShifts
            .AnyAsync(s => s.RoomId == dto.RoomId
                        && s.StatusEnum == DoctorShiftStatus.Active
                        && s.StartTime < (newEnd ?? DateTime.MaxValue)
                        && (s.EndTime == null || s.EndTime > newStart));

        if (roomOverlap)
            return (false, "Phòng này đã có bác sĩ khác trực trùng thời gian. Vui lòng chọn phòng hoặc khung giờ khác.");

        // ── Fix sequence lệch (do Doctor/Index.razor ép Id thủ công) ──
        await ctx.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"DoctorShifts\"', 'Id'), COALESCE(MAX(\"Id\"), 0) + 1, false) FROM \"DoctorShifts\"");

        var shift = new DoctorShift
        {
            DoctorId = dto.DoctorId,
            RoomId = dto.RoomId,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            StatusEnum = DoctorShiftStatus.Active
        };

        ctx.DoctorShifts.Add(shift);
        await ctx.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ══════════════════════════════════════════════════════════
    // 5. XÓA CA TRỰC — Hard delete, trả về false nếu không tìm thấy
    // ══════════════════════════════════════════════════════════
    public async Task<bool> DeleteShiftAsync(int id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var shift = await ctx.DoctorShifts.FindAsync(id);
        if (shift == null) return false;

        ctx.DoctorShifts.Remove(shift);
        await ctx.SaveChangesAsync();
        return true;
    }
}
