# Lịch Làm Việc Bác Sĩ — Implementation Plan

## Mục tiêu

Bác sĩ vào trang "Lịch làm việc" → xem ca được Manager phân → chỉnh Capacity → nhấn Active → lịch public cho lễ tân/user đặt khám.

## Phương án hiển thị: Day-by-Day List (Phương án 2)

Dropdown chọn tuần → hiện danh sách card theo từng ngày → mỗi card = 1 ca trực.

---

## Proposed Changes

### 1. Enum — Sửa `DoctorShiftStatus`

#### [MODIFY] [DoctorShiftStatusEnum.cs](file:///e:/SE/SE7_SP26/PRN222/SmartClinic/Constant/DoctorShiftStatusEnum.cs)

Hiện tại chỉ có `Active = 0`, `Completed = 1`. Cần mở rộng thành 3 trạng thái phản ánh đúng workflow:

```csharp
public enum DoctorShiftStatus : byte
{
    Draft = 0,      // Manager đã phân, BS chưa kích hoạt
    Active = 1,     // BS đã kích hoạt → public cho booking
    Completed = 2   // Ca đã kết thúc (computed hoặc manual)
}
```

> [!IMPORTANT]
> **Draft (0)** là default → tất cả ca trực mới tạo sẽ ở trạng thái Draft.
> Khi BS nhấn Active thì chuyển sang Active (1). Không thể quay lại Draft (one-way).

---

### 2. Entity — Thêm `Status` vào `DoctorShift`

#### [MODIFY] [DoctorShift.cs](file:///e:/SE/SE7_SP26/PRN222/SmartClinic/Models/DoctorShift.cs)

```diff
+using SmartClinic.Constant;
 
 public class DoctorShift
 {
     // ... existing fields ...
+    public DoctorShiftStatus Status { get; set; } = DoctorShiftStatus.Draft;
 }
```

**Lý do**: Dùng enum thay vì `IsActive` bool → rõ ràng hơn, mở rộng dễ.

Đồng thời cập nhật `ComputedStatus` — nếu `Status == Draft` thì trả về "Chờ kích hoạt" thay vì tính theo giờ.

---

### 3. DbContext — Config Fluent API cho Status

#### [MODIFY] [SmartClinicDbContext.cs](file:///e:/SE/SE7_SP26/PRN222/SmartClinic/Models/SmartClinicDbContext.cs)

```diff
 entity.Property(e => e.Capacity).HasDefaultValue(10);
+entity.Property(e => e.Status)
+    .HasConversion<short>()
+    .HasColumnType("smallint")
+    .HasDefaultValue(DoctorShiftStatus.Draft);
```

---

### 4. Migration

Chạy `dotnet ef migrations add AddDoctorShiftStatus` → `dotnet ef database update`

---

### 5. Service — Thêm methods vào `IDoctorShiftService`

#### [MODIFY] [DoctorShiftService.cs](file:///e:/SE/SE7_SP26/PRN222/SmartClinic/Services/DoctorShiftService.cs)

Thêm 2 methods mới:

```csharp
// Lấy ca trực của BS đang đăng nhập, trong khoảng tuần
Task<List<DoctorShiftDisplayDto>> GetMyShiftsAsync(int doctorId, DateTime from, DateTime to);

// BS cập nhật capacity + kích hoạt ca trực
Task<(bool Success, string Error)> ActivateShiftAsync(int shiftId, int doctorId, int capacity);
```

**`GetMyShiftsAsync`**: Chỉ lấy ca có `DoctorId == doctorId`. Include Room, Department, ShiftDefinition. Kèm thêm **BookedCount** (đếm QueueTickets có `DoctorShiftId == shift.Id` và status `Appointment`).

**`ActivateShiftAsync`** — Logic:
1. Validate `shift.DoctorId == doctorId` (chỉ BS sở hữu mới được active)
2. Validate `shift.Status == Draft` (không active lại)
3. Validate `capacity >= bookedCount` (không được nhỏ hơn số đã book)
4. Validate `capacity >= 1` (min)
5. Set `shift.Capacity = capacity`
6. Set `shift.RemainCapacity = capacity - bookedCount`
7. Set `shift.Status = DoctorShiftStatus.Active`

---

### 6. DTO — Thêm field cho Doctor view

#### [MODIFY] [DoctorShiftDto.cs](file:///e:/SE/SE7_SP26/PRN222/SmartClinic/DTOs/DoctorShiftDto.cs)

Thêm vào `DoctorShiftDisplayDto`:

```diff
 public string ShiftName { get; set; } = string.Empty;
+public int BookedCount { get; set; }       // Số BN đã đặt (từ QueueTickets)
+public string ShiftStatus { get; set; } = string.Empty;  // Draft/Active/Completed
```

> [!TIP]
> **RemainCapacity tự tính**: `RemainCapacity = Capacity - BookedCount`. Bác sĩ **không cần tự điền**. Hệ thống tự đếm từ `QueueTickets` có `DoctorShiftId` trỏ tới ca này.

---

### 7. DoctorLayout — Thêm NavLink

#### [MODIFY] [DoctorLayout.razor](file:///e:/SE/SE7_SP26/PRN222/SmartClinic/Components/Layout/DoctorLayout.razor)

Thêm NavLink "Lịch làm việc" vào trong `<ul>` navigation, sau "Dashboard":

```html
<li>
    <NavLink href="/doctor/my-schedule" class="@NavClass("/doctor/my-schedule")">
        <i class="ph ph-calendar-check text-lg"></i>
        <span>Lịch làm việc</span>
    </NavLink>
</li>
```

---

### 8. Page — Tạo `MySchedule.razor`

#### [NEW] [MySchedule.razor](file:///e:/SE/SE7_SP26/PRN222/SmartClinic/Components/Pages/Doctor/MySchedule.razor)

Layout phương án 2 (Day-by-Day List):

**Header**: Dropdown chọn tuần (tuần này, tuần trước, tuần sau, ...)

**Body**: Danh sách ngày trong tuần đã chọn → mỗi ngày hiện các ca:

```
📅 Thứ 2 — 31/03/2026
┌─────────────────────────────────────────────────────┐
│ Ca Sáng (07:30 - 11:30)                             │
│ 🏥 Phòng Nội 1 — Khoa Nội                           │
│                                                      │
│ Số lượng BN tối đa: [___10___]                      │
│ Đã đặt: 3 / 10                                      │
│ Trạng thái: ● Chờ kích hoạt                         │
│                              [🟢 Kích hoạt]         │
└─────────────────────────────────────────────────────┘

📅 Thứ 3 — 01/04/2026
┌─────────────────────────────────────────────────────┐
│ Ca Chiều (13:30 - 17:30)                            │
│ 🏥 Phòng Ngoại 2 — Khoa Ngoại                      │
│                                                      │
│ Số lượng BN tối đa: 15                              │
│ Đã đặt: 5 / 15    Còn lại: 10                      │
│ Trạng thái: ● Đã kích hoạt                         │
└─────────────────────────────────────────────────────┘
```

**Logic chi tiết**:
- Ca `Draft`: Hiện input capacity (editable) + nút "Kích hoạt"
- Ca `Active`: Hiện capacity (readonly) + text "Đã kích hoạt" + progress bar đã đặt/tổng
- Ca `Completed`: Hiện capacity (readonly) + badge "Đã hoàn thành" (mờ)
- Ca quá khứ: Chỉ hiển thị, không cho thao tác

---

### 9. AppointmentService — Filter theo Status

#### [MODIFY] [AppointmentService.cs](file:///e:/SE/SE7_SP26/PRN222/SmartClinic/Services/AppointmentService.cs)

Cập nhật `GetAvailableShiftsAsync` để chỉ trả về ca có `Status == Active`:

```diff
 .Where(s => s.RemainCapacity > 0
-         && s.Date >= today);
+         && s.Date >= today
+         && s.Status == DoctorShiftStatus.Active);
```

> [!WARNING]
> Đây là thay đổi **critical**: Lễ tân/User chỉ thấy ca đã Active. Ca Draft sẽ không hiển thị trong danh sách đặt lịch.

---

## Execution Order

1. Sửa `DoctorShiftStatusEnum.cs` (thêm Draft)
2. Sửa `DoctorShift.cs` (thêm property Status)
3. Sửa `SmartClinicDbContext.cs` (config Fluent API)
4. Chạy migration
5. Sửa `DoctorShiftDto.cs` (thêm BookedCount, ShiftStatus)
6. Sửa `DoctorShiftService.cs` (thêm 2 methods)
7. Sửa `AppointmentService.cs` (filter Active only)
8. Sửa `DoctorLayout.razor` (thêm NavLink)
9. Tạo `MySchedule.razor` (page mới)

## Verification Plan

### Automated Tests
- Build project: `dotnet build`
- Chạy migration: `dotnet ef database update`
- Kiểm tra trên trình duyệt: đăng nhập BS → vào /doctor/my-schedule
  - Xem danh sách ca
  - Chỉnh capacity → nhấn Active
  - Kiểm tra user/lễ tân chỉ thấy ca Active
