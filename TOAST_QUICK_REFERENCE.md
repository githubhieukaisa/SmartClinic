# 📋 Toast Notification - Quick Reference

## One-Liner Usage

```razor
@inject ToastNotificationService Toast

<button @onclick="() => Toast.ShowAsync('Done!', 'success')">Show Toast</button>
```

That's it! No StateHasChanged, no components, no complexity.

---

## All Methods

```csharp
// Generic
await Toast.ShowAsync(message, type);

// Shortcuts
await Toast.ShowSuccessAsync(message);
await Toast.ShowErrorAsync(message);
await Toast.ShowWarningAsync(message);
await Toast.ShowInfoAsync(message);
```

---

## Types

| Type | Icon | Color |
|------|------|-------|
| `"success"` | ✓ | Green |
| `"error"` | ✕ | Red |
| `"warning"` | ⚠ | Orange |
| `"info"` | ℹ | Blue |

---

## Where to Use

- ✅ Pages
- ✅ Components
- ✅ Services
- ✅ SignalR handlers
- ✅ Background tasks

---

## Examples

```razor
// From page button
<button @onclick="OnClick">Click Me</button>

@code {
    private async Task OnClick()
    {
        await Toast.ShowSuccessAsync("Success!");
    }
}
```

```razor
// From form submission
<form @onsubmit="HandleSubmit">
    <input type="text" @bind="name" />
    <button type="submit">Submit</button>
</form>

@code {
    private async Task HandleSubmit()
    {
        await Toast.ShowAsync($"Hello {name}!", "info");
    }
}
```

```csharp
// From a service
public class MyService
{
    private readonly ToastNotificationService _toast;
    
    public MyService(ToastNotificationService toast)
    {
        _toast = toast;
    }
    
    public async Task DoSomethingAsync()
    {
        await _toast.ShowSuccessAsync("Done!");
    }
}
```

```razor
// From SignalR
private async void OnSignalREvent()
{
    await InvokeAsync(async () =>
    {
        await Toast.ShowAsync("Event received!", "info");
    });
}
```

---

## Debug

Open browser console (F12) to see:
```
[NotificationService] Calling JS toast
========== TOAST DEBUG ==========
Toast message: Your message
Toast type: success
Timestamp: 2024-12-19T10:30:45.123Z
=================================
[JS] Toast added to DOM
```

---

## That's All!

No complex setup. Just inject and call. Toast appears. Done! ✨
