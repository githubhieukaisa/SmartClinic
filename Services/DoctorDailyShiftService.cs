using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
using SmartClinic.Models;

namespace SmartClinic.Services;

/// <summary>
/// ═══════════════════════════════════════════════════════════════════
/// DoctorDailyShiftService — Daily Pre-fetching & Local Scheduling
/// ═══════════════════════════════════════════════════════════════════
///
/// Giải quyết vấn đề của cách cũ (15-second polling timer):
///   ❌ Cũ: Cứ 15 giây query DB 1 lần → tốn tài nguyên, delay lên tới 15s.
///   ✅ Mới: Chỉ query DB 1 lần khi bác sĩ đăng nhập. Sau đó tự tính
///          thời điểm bắt đầu/kết thúc ca và set Timer chính xác đến giây.
///
/// Lifecycle:
///   1. InitializeAsync(doctorId) → Query toàn bộ ca trực trong ngày hôm nay.
///   2. EvaluateCurrentState() → Tính trạng thái hiện tại & set timer one-off.
///   3. Khi Timer bắn → EvaluateCurrentState() lại → lặp đến hết ngày.
///   4. DisposeAsync() → Huỷ Timer, tránh memory leak khi bác sĩ đóng tab.
///
/// Đăng ký: Scoped (mỗi Blazor Circuit = 1 instance = 1 bác sĩ).
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
public class DoctorDailyShiftService : IAsyncDisposable
{
    private readonly IDbContextFactory<SmartClinicDbContext> _dbFactory;
    private readonly NotificationService _notification;

    private List<DoctorShift> _todayShifts = new();
    private System.Threading.Timer? _timer;
    private int _doctorId;
    public bool IsInitialized { get; private set; } = false;

    // ── Public State ──────────────────────────────────────────────
    public int? CurrentRoomId { get; private set; }
    public bool IsOnDuty { get; private set; }
    public DoctorShift? CurrentShift { get; private set; }

    /// <summary>Blazor subscribes to this event to call StateHasChanged().</summary>
    public event Action? OnShiftStateChanged;

    public DoctorDailyShiftService(
        IDbContextFactory<SmartClinicDbContext> dbFactory,
        NotificationService notification)
    {
        _dbFactory = dbFactory;
        _notification = notification;
    }

    // ══════════════════════════════════════════════════════════════
    // 1. KHỞI TẠO — Chỉ query DB 1 lần duy nhất
    // ══════════════════════════════════════════════════════════════
    public async Task InitializeAsync(int doctorId)
    {
        // Guard: chỉ chạy 1 lần duy nhất trong suốt vòng đời Scoped (1 circuit = 1 tab)
        if (IsInitialized) return;
        IsInitialized = true;

        _doctorId = doctorId;

        await using var ctx = await _dbFactory.CreateDbContextAsync();

        var today = DateTime.Today;

        _todayShifts = await ctx.DoctorShifts
            .AsNoTracking()
            .Include(s => s.ShiftDefinition)
            .Include(s => s.Room)
            .Where(s => s.DoctorId == _doctorId && s.Date == today)
            .ToListAsync();

        _todayShifts = _todayShifts.OrderBy(s => s.ShiftDefinition.StartTime).ToList();

        System.Diagnostics.Debug.WriteLine(
            $"[DailyShiftService] Loaded {_todayShifts.Count} shifts for Doctor {_doctorId}");

        // Tính trạng thái ngay lần đầu, set timer cho lần chuyển tiếp kế tiếp
        await EvaluateCurrentStateAsync();
    }

    // ══════════════════════════════════════════════════════════════
    // 2. TÍNH TRẠNG THÁI + ĐẶT TIMER ONE-OFF
    // ══════════════════════════════════════════════════════════════
    private async Task EvaluateCurrentStateAsync()
    {
        var now = DateTime.Now;

        // Tìm ca đang diễn ra (StartTime <= now < EndTime)
        var activeShift = _todayShifts
            .FirstOrDefault(s => s.Date.Date.Add(s.ShiftDefinition.StartTime) <= now
                              && s.Date.Date.Add(s.ShiftDefinition.EndTime) > now);

        if (activeShift != null)
        {
            // ── ĐANG TRỰC ──────────────────────────────────────────
            await SetOnDutyAsync(activeShift);

            ScheduleNextTick(activeShift.Date.Date.Add(activeShift.ShiftDefinition.EndTime), now,
                label: $"End of shift in Room {activeShift.RoomId}");
        }
        else
        {
            // ── NGOÀI CA TRỰC ──────────────────────────────────────
            await SetOffDutyAsync();

            // Tìm ca tiếp theo trong ngày hôm nay
            var nextShift = _todayShifts
                .FirstOrDefault(s => s.Date.Date.Add(s.ShiftDefinition.StartTime) > now);

            if (nextShift != null)
            {
                ScheduleNextTick(nextShift.Date.Date.Add(nextShift.ShiftDefinition.StartTime), now,
                    label: $"Start of next shift in Room {nextShift.RoomId}");
            }
            // Nếu không còn ca nào hôm nay → không set timer, nghỉ đến sáng mai
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 3. HELPERS: SET STATE + NOTIFY UI
    // ══════════════════════════════════════════════════════════════
    private async Task SetOnDutyAsync(DoctorShift shift)
    {
        var roomId = shift.RoomId;
        var previousRoom = CurrentRoomId;

        IsOnDuty = true;
        CurrentRoomId = roomId;
        CurrentShift = shift;

        // Chuyển phòng SignalR nếu cần
        if (previousRoom.HasValue && previousRoom.Value != roomId)
        {
            await _notification.LeaveRoomAsync(previousRoom.Value);
            System.Diagnostics.Debug.WriteLine(
                $"[DailyShiftService] Left Room_{previousRoom.Value}");
        }

        if (previousRoom != roomId)
        {
            await _notification.JoinRoomAsync(roomId);
            System.Diagnostics.Debug.WriteLine(
                $"[DailyShiftService] Joined Room_{roomId}");
        }

        OnShiftStateChanged?.Invoke();
        System.Diagnostics.Debug.WriteLine(
            $"[DailyShiftService] ✅ ON DUTY → Room {roomId}");
    }

    private async Task SetOffDutyAsync()
    {
        if (CurrentRoomId.HasValue)
        {
            await _notification.LeaveRoomAsync(CurrentRoomId.Value);
            System.Diagnostics.Debug.WriteLine(
                $"[DailyShiftService] Left Room_{CurrentRoomId.Value} (Off Duty)");
        }

        IsOnDuty = false;
        CurrentRoomId = null;
        CurrentShift = null;

        OnShiftStateChanged?.Invoke();
        System.Diagnostics.Debug.WriteLine("[DailyShiftService] ⬜ OFF DUTY");
    }

    // ══════════════════════════════════════════════════════════════
    // 4. ĐẶT TIMER ONE-OFF (chính xác đến giây, không polling)
    // ══════════════════════════════════════════════════════════════
    private void ScheduleNextTick(DateTime target, DateTime now, string label)
    {
        // Huỷ timer cũ nếu có
        _timer?.Dispose();

        var delay = target - now;

        // Nếu thời điểm đã qua (race condition nhỏ), bắn ngay sau 1 giây
        if (delay <= TimeSpan.Zero) delay = TimeSpan.FromSeconds(1);

        // One-off: period = Timeout.InfiniteTimeSpan → chỉ bắn 1 lần
        _timer = new System.Threading.Timer(
            callback: async _ =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DailyShiftService] ⏰ Timer fired: {label}");
                await EvaluateCurrentStateAsync();
            },
            state: null,
            dueTime: delay,
            period: System.Threading.Timeout.InfiniteTimeSpan
        );

        System.Diagnostics.Debug.WriteLine(
            $"[DailyShiftService] ⏳ Next tick in {delay.TotalMinutes:F1}m → {label}");
    }

    // ══════════════════════════════════════════════════════════════
    // 5. DISPOSE — Bắt buộc để tránh memory leak khi đóng tab
    // ══════════════════════════════════════════════════════════════
    public async ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        _timer = null;
        System.Diagnostics.Debug.WriteLine("[DailyShiftService] Disposed.");
        await ValueTask.CompletedTask;
    }
}
