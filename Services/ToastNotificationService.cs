using Microsoft.JSInterop;

namespace SmartClinic.Services;

/// <summary>
/// JavaScript Toast Notification Service
/// 
/// Simple wrapper around client-side JavaScript toast notifications.
/// Completely decoupled from Blazor component lifecycle.
/// 
/// Usage:
/// - Inject: @inject ToastNotificationService Toast
/// - Call: await Toast.ShowAsync("Message", "info")
/// </summary>
public class ToastNotificationService
{
    private readonly IJSRuntime _jsRuntime;

    public ToastNotificationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        System.Diagnostics.Debug.WriteLine("[ToastNotificationService] Initialized with IJSRuntime");
    }

    /// <summary>
    /// Show a toast notification
    /// </summary>
    public async Task ShowAsync(string message, string type = "info")
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        System.Diagnostics.Debug.WriteLine($"[NotificationService] Calling JS toast");
        System.Diagnostics.Debug.WriteLine($"[NotificationService] Message: {message}");
        System.Diagnostics.Debug.WriteLine($"[NotificationService] Type: {type}");

        try
        {
            await _jsRuntime.InvokeVoidAsync("appNotifications.showToast", message, type);
            System.Diagnostics.Debug.WriteLine($"[NotificationService] JS invocation completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Show success notification
    /// </summary>
    public async Task ShowSuccessAsync(string message)
    {
        await ShowAsync(message, "success");
    }

    /// <summary>
    /// Show error notification
    /// </summary>
    public async Task ShowErrorAsync(string message)
    {
        await ShowAsync(message, "error");
    }

    /// <summary>
    /// Show warning notification
    /// </summary>
    public async Task ShowWarningAsync(string message)
    {
        await ShowAsync(message, "warning");
    }

    /// <summary>
    /// Show info notification
    /// </summary>
    public async Task ShowInfoAsync(string message)
    {
        await ShowAsync(message, "info");
    }
}
