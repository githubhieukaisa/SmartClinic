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
    Task<(List<AutoSchedulePreviewItemDto> Items, string Error)> PreviewAutoScheduleAsync(AutoScheduleRequestDto request);
    Task<AutoScheduleResultDto> ConfirmAutoScheduleAsync(List<AutoSchedulePreviewItemDto> items);

    // ── Doctor Schedule Management ──
    Task<List<DoctorShiftDisplayDto>> GetMyShiftsAsync(int doctorId, DateTime from, DateTime to);
    Task<(bool Success, string Error)> ActivateShiftAsync(int shiftId, int? callerDoctorId);
    Task<(bool Success, string Error)> BatchActivateAsync(int doctorId, DateTime weekStart);
    Task<(bool Success, string Error)> UpdateShiftCapacityAsync(int shiftId, int callerDoctorId, int newCapacity);
    Task<(bool Success, string Error)> BatchUpdateCapacityAsync(int doctorId, DateTime weekStart, int capacity);
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
            ShiftName = s.ShiftDefinition.Name,
            ShiftStatus = s.StatusDisplay
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
            .Include(u => u.Department)
            .Where(u => ((u.RoleMask & 2) == 2 || (u.RoleMask & 32) == 32) && u.IsActive == true)
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
            .Where(r => (r.Flags & RoomFlags.IsActive) != 0)
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
            Capacity = 10,
            RemainCapacity = 10
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
                        Capacity = 10,
                        RemainCapacity = 10
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

    // ══════════════════════════════════════════════════════════
    // 8. PREVIEW PHÂN LỊCH TỰ ĐỘNG (Round-Robin, ưu tiên khoa)
    //    Không ghi DB — chỉ tính toán trong memory
    // ══════════════════════════════════════════════════════════
    public async Task<(List<AutoSchedulePreviewItemDto> Items, string Error)> PreviewAutoScheduleAsync(AutoScheduleRequestDto request)
    {
        // ── Validation ──
        if (request.ToDate.Date < request.FromDate.Date)
            return (new(), "Ngày kết thúc phải >= ngày bắt đầu.");
        if ((request.ToDate.Date - request.FromDate.Date).Days > 13)
            return (new(), "Phạm vi phân lịch tối đa là 2 tuần (14 ngày).");
        if (!request.SelectedDoctorIds.Any())
            return (new(), "Vui lòng chọn ít nhất 1 bác sĩ.");
        if (!request.SelectedRoomIds.Any())
            return (new(), "Vui lòng chọn ít nhất 1 phòng khám.");
        if (!request.SelectedShiftDefinitionIds.Any())
            return (new(), "Vui lòng chọn ít nhất 1 ca trực.");

        // Auto-clamp: nếu FromDate nằm trong quá khứ → tự đẩy lên hôm nay
        var effectiveFrom = request.FromDate.Date < DateTime.Today
            ? DateTime.Today
            : request.FromDate.Date;
        var effectiveTo = request.ToDate.Date;

        if (effectiveFrom > effectiveTo)
            return (new(), "Toàn bộ khoảng ngày đã nằm trong quá khứ.");

        await using var ctx = await _factory.CreateDbContextAsync();

        // ── Load dữ liệu cần thiết ──
        var selectedDoctors = await ctx.Users
            .AsNoTracking()
            .Where(u => request.SelectedDoctorIds.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.DepartmentId, u.RoleMask })
            .ToListAsync();

        var selectedRooms = await ctx.Rooms
            .AsNoTracking()
            .Include(r => r.Department)
            .Where(r => request.SelectedRoomIds.Contains(r.Id))
            .OrderBy(r => r.Department.Name).ThenBy(r => r.Name)
            .ToListAsync();

        var selectedShiftDefs = await ctx.ShiftDefinitions
            .AsNoTracking()
            .Where(s => request.SelectedShiftDefinitionIds.Contains(s.Id))
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        // ── Load ca trực đã tồn tại trong khoảng ngày ──
        var existingShifts = await ctx.DoctorShifts
            .AsNoTracking()
            .Where(s => s.Date >= effectiveFrom && s.Date <= effectiveTo)
            .Select(s => new { s.DoctorId, s.RoomId, s.Date, s.ShiftDefinitionId })
            .ToListAsync();

        // HashSet để check conflict nhanh
        // doctorOccupied: "DoctorId_yyyyMMdd_ShiftDefId" → BS đã bận ở ô nào đó
        // roomOccupied:   "RoomId_yyyyMMdd_ShiftDefId"   → Phòng đã có ca
        var doctorOccupied = new HashSet<string>(
            existingShifts.Select(s => $"{s.DoctorId}_{s.Date:yyyyMMdd}_{s.ShiftDefinitionId}"));
        var roomOccupied = new HashSet<string>(
            existingShifts.Select(s => $"{s.RoomId}_{s.Date:yyyyMMdd}_{s.ShiftDefinitionId}"));

        // ── Round-Robin: phân bác sĩ vào các ô ──
        var result = new List<AutoSchedulePreviewItemDto>();
        int totalDays = (effectiveTo - effectiveFrom).Days + 1;
        int doctorIndex = 0; // Round-Robin pointer
        int skipped = 0;

        for (int dayOffset = 0; dayOffset < totalDays; dayOffset++)
        {
            var currentDate = effectiveFrom.AddDays(dayOffset);

            foreach (var shiftDef in selectedShiftDefs)
            {
                foreach (var room in selectedRooms)
                {
                    var roomKey = $"{room.Id}_{currentDate:yyyyMMdd}_{shiftDef.Id}";
                    bool isOverwrite = false;

                    // Kiểm tra phòng đã có ca chưa
                    if (roomOccupied.Contains(roomKey))
                    {
                        if (!request.OverwriteExisting)
                        {
                            skipped++;
                            continue; // Skip ô đã có ca
                        }
                        isOverwrite = true;

                        // ★ FIX: Giải phóng bác sĩ cũ khi ghi đè
                        // BS cũ sẽ bị thay thế → xóa khỏi doctorOccupied để không chặn logic sau
                        var displacedShift = existingShifts.FirstOrDefault(s =>
                            s.RoomId == room.Id &&
                            s.Date.Date == currentDate.Date &&
                            s.ShiftDefinitionId == shiftDef.Id);
                        if (displacedShift != null)
                        {
                            doctorOccupied.Remove(
                                $"{displacedShift.DoctorId}_{currentDate:yyyyMMdd}_{shiftDef.Id}");
                        }
                    }

                    // ── Tìm bác sĩ phù hợp bằng Round-Robin ──
                    // Bác sĩ (Bit 2) chỉ trực phòng Clinic, KTV (Bit 32) chỉ trực phòng Lab
                    var sortedDoctors = selectedDoctors
                        .Where(d => (room.IsLab && (d.RoleMask & 32) == 32) || (!room.IsLab && (d.RoleMask & 2) == 2))
                        .OrderByDescending(d => d.DepartmentId == room.DepartmentId)
                        .ThenByDescending(d => d.DepartmentId == null)
                        .ThenBy(d => d.FullName)
                        .ToList();

                    if (!sortedDoctors.Any())
                    {
                        skipped++;
                        continue;
                    }

                    bool assigned = false;
                    for (int attempt = 0; attempt < sortedDoctors.Count; attempt++)
                    {
                        var doctor = sortedDoctors[(doctorIndex + attempt) % sortedDoctors.Count];
                        var docKey = $"{doctor.Id}_{currentDate:yyyyMMdd}_{shiftDef.Id}";

                        // ★ FIX: Check cả doctorOccupied (DB) lẫn preview đã gán (cùng HashSet)
                        if (doctorOccupied.Contains(docKey))
                            continue;

                        // ── Gán thành công ──
                        result.Add(new AutoSchedulePreviewItemDto
                        {
                            DoctorId = doctor.Id,
                            DoctorName = doctor.FullName ?? "N/A",
                            RoomId = room.Id,
                            RoomName = room.Name,
                            Date = currentDate,
                            ShiftDefinitionId = shiftDef.Id,
                            ShiftName = shiftDef.Name,
                            IsOverwrite = isOverwrite
                        });

                        // ★ FIX: Cập nhật state cho các vòng lặp tiếp theo
                        doctorOccupied.Add(docKey);   // BS này đã bận ở slot này
                        roomOccupied.Add(roomKey);     // Phòng này đã được gán

                        assigned = true;
                        doctorIndex = (doctorIndex + attempt + 1) % sortedDoctors.Count;
                        break;
                    }

                    if (!assigned) skipped++;
                }
            }
        }

        if (!result.Any())
        {
            var msg = "Không có ô nào có thể phân lịch.";
            if (skipped > 0)
                msg += $" ({skipped} ô bị bỏ qua do đã có ca trực hoặc bác sĩ bị trùng lịch)";
            return (new(), msg);
        }

        return (result, string.Empty);
    }

    // ══════════════════════════════════════════════════════════
    // 9. XÁC NHẬN LƯU PHÂN LỊCH TỰ ĐỘNG
    //    Nhận preview items đã duyệt → ghi vào DB
    // ══════════════════════════════════════════════════════════
    public async Task<AutoScheduleResultDto> ConfirmAutoScheduleAsync(List<AutoSchedulePreviewItemDto> items)
    {
        if (!items.Any())
            return new AutoScheduleResultDto { Success = false, Error = "Không có dữ liệu để lưu." };

        await using var ctx = await _factory.CreateDbContextAsync();
        int created = 0;
        int skipped = 0;

        // Fix sequence PostgreSQL
        await ctx.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"DoctorShifts\"', 'Id'), COALESCE(MAX(\"Id\"), 0) + 1, false) FROM \"DoctorShifts\"");

        foreach (var item in items)
        {
            // Skip ngày quá khứ (phòng trường hợp preview được tạo trước nửa đêm)
            if (item.Date.Date < DateTime.Today)
            {
                skipped++;
                continue;
            }

            // Nếu là overwrite → xóa ca cũ ở phòng này trước
            if (item.IsOverwrite)
            {
                var oldShifts = await ctx.DoctorShifts
                    .Where(s => s.RoomId == item.RoomId
                             && s.Date == item.Date.Date
                             && s.ShiftDefinitionId == item.ShiftDefinitionId)
                    .ToListAsync();
                if (oldShifts.Any())
                {
                    ctx.DoctorShifts.RemoveRange(oldShifts);
                    await ctx.SaveChangesAsync(); // Commit xóa trước để conflict check chính xác
                }
            }

            // Kiểm tra lại conflict lần cuối (tránh race condition)
            var conflict = await ctx.DoctorShifts.AnyAsync(s =>
                s.Date == item.Date.Date &&
                s.ShiftDefinitionId == item.ShiftDefinitionId &&
                (s.DoctorId == item.DoctorId || s.RoomId == item.RoomId));

            if (conflict)
            {
                skipped++;
                continue;
            }

            ctx.DoctorShifts.Add(new DoctorShift
            {
                DoctorId = item.DoctorId,
                RoomId = item.RoomId,
                Date = item.Date.Date,
                ShiftDefinitionId = item.ShiftDefinitionId,
                Capacity = 10
            });
            created++;
        }

        await ctx.SaveChangesAsync();

        return new AutoScheduleResultDto
        {
            Success = true,
            TotalCreated = created,
            TotalSkipped = skipped,
            Summary = $"Đã tạo {created} ca trực thành công" + (skipped > 0 ? $", bỏ qua {skipped} ca trùng lịch." : ".")
        };
    }

    // ══════════════════════════════════════════════════════════
    // 10. LẤY CA TRỰC CỦA BÁC SĨ (Doctor Schedule)
    //     Kèm BookedCount = đếm QueueTickets chưa hủy
    // ══════════════════════════════════════════════════════════
    public async Task<List<DoctorShiftDisplayDto>> GetMyShiftsAsync(int doctorId, DateTime from, DateTime to)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var shifts = await ctx.DoctorShifts
            .AsNoTracking()
            .Include(s => s.Room).ThenInclude(r => r.Department)
            .Include(s => s.ShiftDefinition)
            .Where(s => s.DoctorId == doctorId && s.Date >= from.Date && s.Date <= to.Date)
            .OrderBy(s => s.Date).ThenBy(s => s.ShiftDefinition.SortOrder)
            .ToListAsync();

        // Đếm số BN đã đặt cho mỗi ca (tất cả ticket chưa hủy)
        var shiftIds = shifts.Select(s => s.Id).ToList();
        var bookedCounts = await ctx.QueueTickets
            .Where(t => t.DoctorShiftId != null
                      && shiftIds.Contains(t.DoctorShiftId.Value)
                      && t.StatusEnum != TicketStatus.Cancelled)
            .GroupBy(t => t.DoctorShiftId)
            .Select(g => new { ShiftId = g.Key, Count = g.Count() })
            .ToListAsync();
        var bookedMap = bookedCounts.ToDictionary(x => x.ShiftId!.Value, x => x.Count);

        return shifts.Select(s =>
        {
            bookedMap.TryGetValue(s.Id, out int booked);
            return new DoctorShiftDisplayDto
            {
                Id = s.Id,
                DoctorId = s.DoctorId,
                RoomId = s.RoomId,
                RoomName = s.Room.Name,
                DepartmentName = s.Room.Department.Name,
                StartTime = s.Date.Date.Add(s.ShiftDefinition.StartTime),
                EndTime = s.Date.Date.Add(s.ShiftDefinition.EndTime),
                Capacity = s.Capacity,
                ShiftName = s.ShiftDefinition.Name,
                Status = s.ComputedStatus,
                BookedCount = booked,
                ShiftStatus = s.StatusDisplay
            };
        }).ToList();
    }

    // ══════════════════════════════════════════════════════════
    // 11. KÍCH HOẠT 1 CA TRỰC (Doctor hoặc Manager)
    //     callerDoctorId = null → Manager (bỏ qua check ownership)
    //     callerDoctorId = X   → Doctor (chỉ active ca của mình)
    // ══════════════════════════════════════════════════════════
    public async Task<(bool Success, string Error)> ActivateShiftAsync(int shiftId, int? callerDoctorId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var shift = await ctx.DoctorShifts.FirstOrDefaultAsync(s => s.Id == shiftId);
        if (shift == null)
            return (false, "Ca trực không tồn tại.");

        if (callerDoctorId.HasValue && shift.DoctorId != callerDoctorId.Value)
            return (false, "Bạn không có quyền thao tác trên ca trực này.");

        if (shift.StatusEnum != DoctorShiftStatus.Draft)
            return (false, "Ca trực đã được kích hoạt trước đó.");

        shift.StatusEnum = DoctorShiftStatus.Active;
        await ctx.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ══════════════════════════════════════════════════════════
    // 12. KÍCH HOẠT TẤT CẢ CA DRAFT TRONG TUẦN
    // ══════════════════════════════════════════════════════════
    public async Task<(bool Success, string Error)> BatchActivateAsync(int doctorId, DateTime weekStart)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var weekEnd = weekStart.AddDays(6);

        var draftShifts = await ctx.DoctorShifts
            .Where(s => s.DoctorId == doctorId
                      && s.Date >= weekStart.Date
                      && s.Date <= weekEnd.Date
                      && s.StatusEnum == DoctorShiftStatus.Draft)
            .ToListAsync();

        if (!draftShifts.Any())
            return (false, "Không có ca trực nào cần kích hoạt trong tuần này.");

        foreach (var shift in draftShifts)
        {
            shift.StatusEnum = DoctorShiftStatus.Active;
        }

        await ctx.SaveChangesAsync();
        return (true, $"Đã kích hoạt {draftShifts.Count} ca trực.");
    }

    // ══════════════════════════════════════════════════════════
    // 13. SỬA CAPACITY 1 CA TRỰC
    //     Tăng/giảm đều được, nhưng không < BookedCount
    //     Không sửa ca đang diễn ra hoặc đã hoàn thành
    // ══════════════════════════════════════════════════════════
    public async Task<(bool Success, string Error)> UpdateShiftCapacityAsync(int shiftId, int callerDoctorId, int newCapacity)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var shift = await ctx.DoctorShifts
            .Include(s => s.ShiftDefinition)
            .FirstOrDefaultAsync(s => s.Id == shiftId);

        if (shift == null)
            return (false, "Ca trực không tồn tại.");

        if (shift.DoctorId != callerDoctorId)
            return (false, "Bạn không có quyền thao tác trên ca trực này.");

        if (newCapacity < 1)
            return (false, "Số lượng bệnh nhân tối đa phải >= 1.");

        // Không sửa ca đang diễn ra
        if (shift.StatusEnum == DoctorShiftStatus.Active && shift.ComputedStatus == "Đang trực")
            return (false, "Không thể chỉnh sửa ca trực đang diễn ra.");

        if (shift.StatusEnum == DoctorShiftStatus.Completed)
            return (false, "Không thể chỉnh sửa ca trực đã hoàn thành.");

        // Đếm số BN đã đặt
        var bookedCount = await ctx.QueueTickets
            .CountAsync(t => t.DoctorShiftId == shiftId
                          && t.StatusEnum != TicketStatus.Cancelled);

        if (newCapacity < bookedCount)
            return (false, $"Không thể đặt số lượng = {newCapacity}. Hiện đã có {bookedCount} bệnh nhân đặt lịch.");

        shift.Capacity = newCapacity;
        shift.RemainCapacity = newCapacity - bookedCount;

        await ctx.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ══════════════════════════════════════════════════════════
    // 14. ĐẶT CAPACITY HÀNG LOẠT CHO CA DRAFT TRONG TUẦN
    //     Chỉ áp dụng cho ca Draft (chưa có ai đặt)
    // ══════════════════════════════════════════════════════════
    public async Task<(bool Success, string Error)> BatchUpdateCapacityAsync(int doctorId, DateTime weekStart, int capacity)
    {
        if (capacity < 1)
            return (false, "Số lượng bệnh nhân tối đa phải >= 1.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var weekEnd = weekStart.AddDays(6);

        var draftShifts = await ctx.DoctorShifts
            .Where(s => s.DoctorId == doctorId
                      && s.Date >= weekStart.Date
                      && s.Date <= weekEnd.Date
                      && s.StatusEnum == DoctorShiftStatus.Draft)
            .ToListAsync();

        if (!draftShifts.Any())
            return (false, "Không có ca trực Draft nào trong tuần để cập nhật.");

        foreach (var shift in draftShifts)
        {
            shift.Capacity = capacity;
            shift.RemainCapacity = capacity;
        }

        await ctx.SaveChangesAsync();
        return (true, $"Đã cập nhật capacity = {capacity} cho {draftShifts.Count} ca trực.");
    }
}
