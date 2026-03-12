# 📖 Notification System - Quick Reference Guide

## Files & Their Roles

### Backend Services

| File | Purpose | Lifetime | Key Method |
|------|---------|----------|-----------|
| `Services/GlobalNotificationService.cs` | State store | **Singleton** | `.Show(message, type)` |
| `Services/ToastNotificationService.cs` | Event publisher | Scoped | `.ShowSuccessAsync(msg, url)` |
| `Services/NotificationService.cs` | SignalR client | Scoped | `.EnsureStartedAsync()` |
| `Hubs/PatientHub.cs` | SignalR hub | Transient | `.SendAsync("event", data)` |

### Frontend Components

| File | Purpose | Role |
|------|---------|------|
| `Web/Components/ToastHost.razor` | Toast container | Event → JS bridge |
| `Web/Layout/DoctorLayout.razor` | Global layout | Includes `<ToastHost />` |
| `Web/Pages/Doctor/*.razor` | Pages | Inject services, call methods |
| `wwwroot/js/notifications.js` | Toast UI | DOM + animations |

---

## Quick Usage Examples

### Show Toast from Page

```razor
@inject ToastNotificationService Toast

<button @onclick="HandleClick">Click Me</button>

@code {
    private async Task HandleClick()
    {
        // Simple
        await Toast.ShowSuccessAsync("Done!");
        
        // With navigation
        await Toast.ShowSuccessAsync("Go to queue", "/doctor/my-patients");
        
        // Other types
        await Toast.ShowErrorAsync("Failed!");
        await Toast.ShowWarningAsync("Warning!");
        await Toast.ShowInfoAsync("Info!");
    }
}
```

### Show Toast from Service

```csharp
public class PatientService
{
    private readonly GlobalNotificationService _notification;
    
    public async Task DoSomething()
    {
        // Work...
        
        // Add to state store (thread-safe)
        _notification.Show("Done!", "success");
        // Automatically picked up by ToastHost
    }
}
```

### Listen to SignalR Events

```razor
@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SignalRService.EnsureStartedAsync();
            
            var connection = SignalRService._hubContext;
            
            connection?.On("QueueTicketCreated", async (string patientName) =>
            {
                await ToastService.ShowSuccessAsync(
                    $"✓ {patientName} added",
                    "/doctor/my-patients"
                );
            });
        }
    }
}
```

---

## Data Flow Diagram

```
User Action (Click Button)
            ↓
Page Component
            ↓
Toast.ShowSuccessAsync("Message")
            ↓
OnToast Event Raised
            ↓
ToastHost Event Handler
            ↓
JS.InvokeVoidAsync("appToasts.show")
            ↓
window.appToasts.show()
            ↓
Create DOM Elements
            ↓
Apply CSS Classes
            ↓
Add Animations
            ↓
Toast Visible on Screen! ✓
```

---

## Toast Types

| Type | CSS Color | Icon | Usage |
|------|-----------|------|-------|
| `success` | Green | ✓ | Operation completed successfully |
| `error` | Red | ✕ | Operation failed |
| `warning` | Orange | ⚠ | Warning/caution message |
| `info` | Blue | ℹ | Informational message |

---

## Service Methods Reference

### GlobalNotificationService

```csharp
// Add notification to state store
void Show(string message, string type = "info")

// Get all notifications
IReadOnlyList<Notification> GetNotifications()

// Remove specific notification
void Remove(string notificationId)

// Remove all notifications
void ClearAll()

// Get count
int NotificationCount

// Events (optional)
event Action OnNotificationAdded
event Action OnNotificationRemoved
```

### ToastNotificationService

```csharp
// Core method
Task ShowToastAsync(string message, string type, string? url)

// Convenience methods
Task ShowSuccessAsync(string message, string? url = null)
Task ShowErrorAsync(string message, string? url = null)
Task ShowWarningAsync(string message, string? url = null)
Task ShowInfoAsync(string message, string? url = null)

// Event
event Func<string, string, string?, Task> OnToast
```

### NotificationService

```csharp
// Ensure connection started
Task EnsureStartedAsync()

// Check if connected
bool IsConnected

// Access hub connection
HubConnection _hubConnection
```

---

## Program.cs Setup

```csharp
// Register services
builder.Services.AddScoped<ToastNotificationService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddSingleton<GlobalNotificationService>();

// Add SignalR
builder.Services.AddSignalR();
```

---

## Common Patterns

### Pattern 1: Try-Catch with Error Toast

```csharp
try
{
    await PatientService.AddAsync(...);
    await Toast.ShowSuccessAsync("Added successfully!");
}
catch (Exception ex)
{
    await Toast.ShowErrorAsync($"Error: {ex.Message}");
}
```

### Pattern 2: Background Operation Notification

```csharp
// Service (background)
var success = await _db.SaveChangesAsync() > 0;
if (success)
{
    _globalNotification.Show("Saved!", "success");
    // Works from any thread, automatically shows in UI
}
```

### Pattern 3: Real-Time SignalR Notification

```csharp
// Backend: Send event
await _hubContext.Clients.User(doctorId)
    .SendAsync("PatientAdmitted", patientName);

// Frontend: Listen in ToastHost
connection?.On("PatientAdmitted", async (string name) =>
{
    await Toast.ShowInfoAsync($"{name} admitted");
});
```

### Pattern 4: Navigation Toast

```csharp
// Click toast to navigate
await Toast.ShowSuccessAsync(
    "Patient added to queue",
    "/doctor/my-patients"  // URL parameter
);
// User clicks toast → navigates
```

---

## Configuration

### In notifications.js

```javascript
window.appToasts.config = {
    duration: 4000,           // Auto-dismiss milliseconds
    toastContainerId: 'toast-container',
    overlayId: 'toast-overlay',
    useOverlay: true,         // Show backdrop
    enableLogging: false      // Disable console logs
}
```

### Adjust Appearance

```javascript
// Change duration
window.appToasts.config.duration = 6000;  // 6 seconds

// Disable overlay
window.appToasts.config.useOverlay = false;

// Enable debugging
window.appToasts.config.enableLogging = true;
```

---

## Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| Toast not showing | Missing `<ToastHost />` | Add to DoctorLayout.razor |
| Toast not showing | Missing `notifications.js` | Add `<script src="js/notifications.js"></script>` |
| Too many console logs | Logging enabled | Set `enableLogging: false` |
| Overlay blocks clicks | pointer-events not disabled | Already fixed, update notifications.js |
| Toast doesn't auto-dismiss | Timer issues | Check if hovering (pauses timer) |
| Navigation doesn't work | URL not set | Pass URL as 3rd param |

---

## JavaScript Toast API

```javascript
// Basic usage
window.appToasts.show(message, type, url);

// Examples
window.appToasts.show("Success!", "success");
window.appToasts.show("Error!", "error", "/error-page");
window.appToasts.show("Warning!", "warning");
window.appToasts.show("Info!", "info", "/info-page");

// Direct method calls
window.appToasts.removeAllToasts();      // Close all
window.appToasts.hideOverlay();           // Hide backdrop
window.appToasts.showOverlay();           // Show backdrop
```

---

## Toast Styling Classes

### Container (Auto-Applied)

```css
fixed top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2
z-[9999] flex flex-col gap-4 max-w-2xl w-11/12
```

### Toast (Auto-Applied)

```css
bg-white border shadow-lg rounded-xl p-4 flex items-center gap-3
w-full transition-all duration-300 pointer-events-auto

/* Type-specific */
/* Success: border-green-300 bg-green-50 */
/* Error:   border-red-300 bg-red-50 */
/* Warning: border-amber-300 bg-amber-50 */
/* Info:    border-blue-300 bg-blue-50 */
```

### Overlay (Auto-Applied)

```css
fixed inset-0 z-[9998] backdrop-blur-sm bg-black/30
transition-opacity duration-300 pointer-events-auto
```

---

## Component Checklist

- [ ] `GlobalNotificationService` registered as Singleton
- [ ] `ToastNotificationService` registered as Scoped
- [ ] `NotificationService` registered as Scoped
- [ ] `<ToastHost />` added to DoctorLayout.razor
- [ ] `notifications.js` included in App.razor
- [ ] SignalR hub configured (PatientHub.cs)
- [ ] Services injected in pages
- [ ] Try-catch around async operations
- [ ] URLs set for navigation toasts
- [ ] SignalR listeners registered in ToastHost
- [ ] Logging disabled in production

---

## Performance Tips

1. **Don't create excessive toasts** - Queue clears after auto-dismiss
2. **Use appropriate types** - Colors convey meaning
3. **Keep messages short** - Toast width is limited
4. **Include URLs for context** - Let users navigate
5. **Avoid nested navigation** - Redirect once

---

## Testing Checklist

```javascript
// In browser console (F12)

// Test 1: Show each type
window.appToasts.show("Success!", "success");
window.appToasts.show("Error!", "error");
window.appToasts.show("Warning!", "warning");
window.appToasts.show("Info!", "info");

// Test 2: Multiple toasts
for(let i=0; i<3; i++) 
    window.appToasts.show(`Toast ${i+1}`, "info");

// Test 3: Navigation
window.appToasts.show("Click me!", "success", "/doctor/my-patients");

// Test 4: Manual dismiss
window.appToasts.removeAllToasts();

// Test 5: Check config
console.log(window.appToasts.config);

// Test 6: Check container in DOM
console.log(document.getElementById('toast-container'));
```

---

## Best Practices Summary

✅ **DO:**
- Use ToastNotificationService in components
- Use GlobalNotificationService in services
- Register listeners in OnAfterRenderAsync (not OnInitialized)
- Unsubscribe from events in DisposeAsync
- Use try-catch with error toasts
- Set URLs for important notifications
- Keep messages concise
- Use type-specific convenience methods

❌ **DON'T:**
- Call JS in OnInitialized (prerendering)
- Forget to unsubscribe (memory leak)
- Show too many toasts at once
- Use dynamic CSS class names
- Log excessively in production
- Navigate without user action
- Use notifications for everything

---

**Last Updated**: 2024  
**Version**: 1.0  
**Status**: Production Ready ✅
