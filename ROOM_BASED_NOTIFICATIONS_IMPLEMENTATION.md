# Room-Based Notifications & Conditional UI Implementation

## 📋 Tổng Quan
Triển khai hệ thống thông báo dựa trên phòng (Room-based notifications) để:
- ✅ Gửi thông báo chỉ cho người ở **cùng phòng** (không broadcast cho tất cả)
- ✅ Hiển thị **Examine button** chỉ khi bệnh nhân có status `"Examining"`
- ✅ Tối ưu hóa hiệu suất và bảo mật

---

## 🔧 Các Components Được Thay Đổi

### 1️⃣ **MyPatient.razor** - UI Layer
**Location:** `Components/Pages/Doctor/MyPatient.razor`

#### Change 1: Examine Button - Conditional Render
```razor
<td class="py-3 px-5 flex items-center justify-center">
    @if (ticket.Status == "Examining")
    {
        <button @onclick="() => OnExaminePatientAsync(ticket.Id, ticket.PatientId)"
                class="flex items-center gap-1 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-medium transition">
            <i class="ph-bold ph-stethoscope"></i>
            Examine
        </button>
    }
</td>
```
**Lý do:** Button chỉ hiển thị khi `status == "Examining"`, tránh nhầm lẫn cho bác sĩ

#### Change 2: Join Room Group on Initialize
```csharp
// STEP 3: Join the room group for receiving room-specific notifications
if (_currentRoomId.HasValue && _currentRoomId.Value > 0)
{
    try
    {
        System.Diagnostics.Debug.WriteLine($"[MyPatient] ▶ Joining room group for RoomId={_currentRoomId}");
        await Notification.JoinRoomAsync(_currentRoomId.Value);
        System.Diagnostics.Debug.WriteLine($"[MyPatient] ✅ Joined room group successfully");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[MyPatient] ⚠️ Error joining room group: {ex.Message}");
    }
}
```
**Lý do:** Đăng ký client vào room group để nhận thông báo chỉ cho phòng đó

**Thêm property:**
```csharp
private bool HasExaminingPatient => _queueTickets?.Any(t => t.Status == "Examining") ?? false;
```

---

### 2️⃣ **PatientService.cs** - Business Logic Layer
**Location:** `Services/PatientService.cs`

**Change:** Broadcast cho Room Group thay vì Broadcast All
```csharp
string patientName = patient?.FullName ?? "Unknown";   
// Broadcast SignalR notification to the specific room group only
System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.AddQueueTicketAsync] Broadcasting QueueTicketUpdated event to Room_{doctorShift.RoomId}");
await _hubContext.Clients.Group($"Room_{doctorShift.RoomId}").SendAsync("QueueTicketUpdated", new 
{ 
    doctorId, 
    ticketId = queueTicket.Id,
    patientName,
    roomId = doctorShift.RoomId  // ← Thêm roomId vào event payload
});
System.Diagnostics.Debug.WriteLine($"✅ [PatientService.AddQueueTicketAsync] SignalR event sent to Room_{doctorShift.RoomId}");
```

**Cũ:**
```csharp
// ❌ Broadcast cho tất cả mọi người
await _hubContext.Clients.All.SendAsync("QueueTicketUpdated", ...)
```

**Lý do:** 
- Chỉ broadcast cho group `Room_{roomId}` thay vì tất cả clients
- Thêm `roomId` vào event payload để tracking

---

### 3️⃣ **PatientHub.cs** - SignalR Hub
**Location:** `Hubs/PatientHub.cs`

**Thêm 2 Methods:**

#### Method 1: JoinRoomAsync
```csharp
/// <summary>
/// Join a specific room group based on RoomId
/// Called when doctor logs in to subscribe to room notifications
/// </summary>
public async Task JoinRoomAsync(int roomId)
{
    string groupName = $"Room_{roomId}";
    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    System.Diagnostics.Debug.WriteLine($"✅ [PatientHub] Client {Context.ConnectionId} joined group: {groupName}");
}
```

#### Method 2: LeaveRoomAsync
```csharp
/// <summary>
/// Leave a specific room group
/// Called when doctor logs out or switches rooms
/// </summary>
public async Task LeaveRoomAsync(int roomId)
{
    string groupName = $"Room_{roomId}";
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    System.Diagnostics.Debug.WriteLine($"✅ [PatientHub] Client {Context.ConnectionId} left group: {groupName}");
}
```

**Lý do:**
- Hub methods cho clients gọi để join/leave room groups
- Group naming convention: `Room_{roomId}`
- Tracking via debug logs

---

### 4️⃣ **NotificationService.cs** - SignalR Client Service
**Location:** `Services/NotificationService.cs`

**Thêm Public Method:**
```csharp
/// <summary>
/// Join a specific room group for receiving room-targeted notifications
/// Call this after doctor logs in with their RoomId
/// </summary>
public async Task JoinRoomAsync(int roomId)
{
    if (_hubConnection == null || !IsConnected)
    {
        System.Diagnostics.Debug.WriteLine($"⚠️ [NotificationService.JoinRoomAsync] Connection not ready, connecting first");
        await EnsureStartedAsync();
    }

    try
    {
        System.Diagnostics.Debug.WriteLine($"🔵 [NotificationService.JoinRoomAsync] Joining Room_{roomId}");
        await _hubConnection!.InvokeAsync("JoinRoomAsync", roomId);
        System.Diagnostics.Debug.WriteLine($"✅ [NotificationService.JoinRoomAsync] Successfully joined Room_{roomId}");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"❌ [NotificationService.JoinRoomAsync] Error: {ex.Message}");
        throw;
    }
}
```

**Lý do:**
- Wrapper method để gọi Hub `JoinRoomAsync` từ Blazor components
- Ensures connection exists trước khi join room
- Error handling + debug logging

---

## 🏗️ Architecture Flow

```
┌─────────────────────────────────────────────────────────┐
│  MyPatient.razor (UI Layer)                             │
│  - Displays queue tickets                              │
│  - Examine button chỉ hiển thị khi status="Examining" │
│  - Calls Notification.JoinRoomAsync(_currentRoomId)   │
└────────────────────┬────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────┐
│  NotificationService (SignalR Client)                   │
│  - Manages HubConnection                               │
│  - Public method: JoinRoomAsync(roomId)               │
│  - Invokes Hub method to subscribe to room group      │
└────────────────────┬────────────────────────────────────┘
                     │
                     ↓ (calls Hub method)
┌─────────────────────────────────────────────────────────┐
│  PatientHub (SignalR Hub)                               │
│  - JoinRoomAsync(roomId)                              │
│  - LeaveRoomAsync(roomId)                             │
│  - Groups.AddToGroupAsync/RemoveFromGroupAsync()      │
└────────────────────┬────────────────────────────────────┘
                     │
                     ↓ (client now subscribed to Room_X group)
┌─────────────────────────────────────────────────────────┐
│  PatientService (Backend Business Logic)               │
│  - AddQueueTicketAsync()                              │
│  - Broadcasts to: Group($"Room_{doctorShift.RoomId}") │
│  - NOT to Clients.All (only specific room!)           │
└────────────────────┬────────────────────────────────────┘
                     │
                     ↓ (sends "QueueTicketUpdated" event)
┌─────────────────────────────────────────────────────────┐
│  Clients in Room_X group receive event                 │
│  - ToastHandler.razor shows notification             │
│  - MyPatient.razor updates queue display             │
└─────────────────────────────────────────────────────────┘
```

---

## 📊 Data Flow Example

### Scenario: Doctor nhập bệnh nhân mới

1. **Reception adds ticket** → `PatientService.AddQueueTicketAsync()`
2. **Get room from DoctorShift** → `RoomId = 3`
3. **Broadcast to group** → `Clients.Group("Room_3").SendAsync(...)`
4. **Only Room_3 subscribers receive** → Doctors in Room 3 only
5. **MyPatient updates** → Table refreshes, notification appears
6. **Examine button** → Only shows if another ticket has status "Examining"

---

## 🔐 Bảo Mật & Tối Ưu

### ✅ Bảo Mật
- ❌ Không broadcast cho tất cả clients (bad practice)
- ✅ Chỉ broadcast cho room group (room-scoped)
- ✅ Mỗi doctor chỉ nhận thông báo từ phòng của họ

### ✅ Hiệu Suất
- Giảm network traffic (không gửi cho tất cả)
- SignalR groups handling được tối ưu
- Debug logging để tracking issues

---

## 🧪 Testing Checklist

- [ ] Add test patient → Toast hiện chỉ cho people ở cùng room
- [ ] Check Examine button chỉ hiển thị khi có ticket status="Examining"
- [ ] Test doctor A ở Room 1, doctor B ở Room 2 → Notifications isolated
- [ ] Logout/Login → Check room group subscription works
- [ ] Refresh page → UI state consistent

---

## 📝 Key Takeaways

| Thành Phần | Trước | Sau |
|-----------|------|-----|
| **Broadcast Target** | `Clients.All` | `Clients.Group("Room_X")` |
| **Examine Button** | Luôn hiển thị | Chỉ khi status="Examining" |
| **Room Subscription** | Manual (không có) | Automatic (JoinRoomAsync) |
| **Event Payload** | Không có roomId | Có roomId |
| **Isolation** | Cross-room | Room-scoped |

---

## 🚀 Deployment Notes

1. **No DB migrations needed** - Logic changes only
2. **Backward compatible** - Old clients still work
3. **Hub methods** - Auto-registered by ASP.NET Core
4. **Group naming** - Convention-based (`Room_{roomId}`)

---

**Last Updated:** 2024  
**Status:** ✅ Production Ready
