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
    Task<List<ShiftDefinition>> GetShiftDefinitionsAsync();
    Task<(bool Success, string Error)> BulkSaveShiftsAsync(List<DoctorShiftWeeklyUpdateDto> updates);
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

        var shifts = await ctx.DoctorShifts
            .AsNoTracking()
            .Include(s => s.Doctor)
            .Include(s => s.Room)
                .ThenInclude(r => r.Department)
            .Include(s => s.ShiftDefinition)
            .Where(s => s.Date >= fromDate && s.Date < toDate)
            .OrderBy(s => s.Date).ThenBy(s => s.ShiftDefinition.StartTime)
            .ToListAsync();

        return shifts.Select(s => new DoctorShiftDisplayDto
        {
            Id = s.Id,
            DoctorId = s.DoctorId,
            DoctorName = s.Doctor.FullName ?? s.Doctor.Username,
            RoomId = s.RoomId,
            RoomName = s.Room.Name,
            DepartmentName = s.Room.Department.Name,
            StartTime = s.Date.Date.Add(s.ShiftDefinition.StartTime),
            EndTime = s.Date.Date.Add(s.ShiftDefinition.EndTime),
            Status = s.ComputedStatus,
            Capacity = s.Capacity,
            ShiftName = s.ShiftDefinition.Name
        }).ToList();
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
        if (dto.Date < DateTime.Today)
            return (false, "Không thể phân lịch trực cho ngày trong quá khứ.");

        await using var ctx = await _factory.CreateDbContextAsync();

        var doctorOverlap = await ctx.DoctorShifts
            .AnyAsync(s => s.DoctorId == dto.DoctorId
                        && s.Date == dto.Date.Date
                        && s.ShiftDefinitionId == dto.ShiftDefinitionId);

        if (doctorOverlap)
            return (false, "Bác sĩ này đã có ca trực trùng thời gian. Vui lòng chọn ca khác.");

        var roomOverlap = await ctx.DoctorShifts
            .AnyAsync(s => s.RoomId == dto.RoomId
                        && s.Date == dto.Date.Date
                        && s.ShiftDefinitionId == dto.ShiftDefinitionId);

        if (roomOverlap)
            return (false, "Phòng này đã có bác sĩ khác trực trùng ca. Vui lòng chọn phòng/ca khác.");

        // Lấy db pattern chuẩn nhất, fix sequence
        await ctx.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"DoctorShifts\"', 'Id'), COALESCE(MAX(\"Id\"), 0) + 1, false) FROM \"DoctorShifts\"");

        // Sinh DoctorShift và tự động sinh 10 Slot nhờ Capacity
        var shift = new DoctorShift
        {
            DoctorId = dto.DoctorId,
            RoomId = dto.RoomId,
            Date = dto.Date.Date,
            ShiftDefinitionId = dto.ShiftDefinitionId,
            Capacity = 10
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
    // ══════════════════════════════════════════════════════════
    // 6. LẤY DANH SÁCH CA TRỰC (ShiftDefinitions)
    // ══════════════════════════════════════════════════════════
    public async Task<List<ShiftDefinition>> GetShiftDefinitionsAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ShiftDefinitions.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync();
    }

    // ══════════════════════════════════════════════════════════
    // 7. LƯU LỊCH TUẦN (BULK SAVE)
    // ══════════════════════════════════════════════════════════
    public async Task<(bool Success, string Error)> BulkSaveShiftsAsync(List<DoctorShiftWeeklyUpdateDto> updates)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        foreach (var update in updates)
        {
            if (update.Date.Date < DateTime.Today)
            {
                return (false, $"Không thể thao tác trên ca trực trong quá khứ (Ngày {update.Date:dd/MM/yyyy}).");
            }

            if (update.IsDeleted)
            {
                if (update.Id > 0)
                {
                    var shift = await ctx.DoctorShifts.Include(s => s.ShiftDefinition).FirstOrDefaultAsync(s => s.Id == update.Id);
                    if (shift != null)
                    {
                        if (shift.ComputedStatus == "Đã hoàn thành")
                        {
                            return (false, $"Không thể xóa ca trực ngày {shift.Date:dd/MM/yyyy} vì ca trực này đã hoàn thành.");
                        }

                        ctx.DoctorShifts.Remove(shift);
                    }
                }
            }
            else
            {
                if (update.Id == 0)
                {
                    // Thêm mới
                    // KT trùng lịch
                    var overlap = await ctx.DoctorShifts.AnyAsync(s => 
                        s.Date == update.Date.Date && s.ShiftDefinitionId == update.ShiftDefinitionId &&
                        (s.DoctorId == update.DoctorId || s.RoomId == update.RoomId));
                    if (overlap) return (false, $"Phát hiện trùng lịch tại ngày {update.Date:dd/MM/yyyy}. Một bác sĩ hoặc phòng không thể có 2 ca trùng thời gian.");

                    var shift = new DoctorShift
                    {
                        DoctorId = update.DoctorId,
                        RoomId = update.RoomId,
                        Date = update.Date.Date,
                        ShiftDefinitionId = update.ShiftDefinitionId,
                        Capacity = 10
                    };
                    ctx.DoctorShifts.Add(shift);
                }
                else
                {
                    // Cập nhật existing (thường chỉ update DoctorId)
                    var shift = await ctx.DoctorShifts.Include(s => s.ShiftDefinition).FirstOrDefaultAsync(s => s.Id == update.Id);
                    if (shift != null && shift.DoctorId != update.DoctorId)
                    {
                        if (shift.ComputedStatus == "Đã hoàn thành")
                        {
                            return (false, $"Không thể chuyển đổi bác sĩ ở ca trực ngày {shift.Date:dd/MM/yyyy} vì ca trực này đã hoàn thành.");
                        }

                        var overlap = await ctx.DoctorShifts.AnyAsync(s => s.Id != update.Id && s.DoctorId == update.DoctorId && s.Date == update.Date.Date && s.ShiftDefinitionId == update.ShiftDefinitionId);
                        if (overlap) return (false, $"Bác sĩ mới được chọn đã có lịch trực khác trong cùng thời gian vào ngày {update.Date:dd/MM/yyyy}.");

                        shift.DoctorId = update.DoctorId;
                    }
                }
            }
        }

        await ctx.SaveChangesAsync();
        return (true, string.Empty);
    }
}
