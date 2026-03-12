using System;
using System.Threading.Tasks;

namespace SmartClinic.Services;

/// <summary>
/// Toast Notification Service - Event Publisher Pattern
/// 
/// Handles toast notification events that components subscribe to.
/// Supports optional URL for clickable toasts with navigation.
/// 
/// Usage:
///   await toastService.ShowToastAsync("Message", "info");
///   await toastService.ShowToastAsync("Message", "success", "/doctor/queue");
/// </summary>
public class ToastNotificationService
{
    /// <summary>
    /// Event raised when a toast should be shown
    /// Signature: (message: string, type: string, url: string?) => Task
    /// </summary>
    public event Func<string, string, string?, Task>? OnToast;

    /// <summary>
    /// Show a toast notification with optional URL for navigation
    /// </summary>
    public async Task ShowToastAsync(string message, string type = "info", string? url = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        System.Diagnostics.Debug.WriteLine(
            $"[ToastService] Show: message='{message}', type='{type}', url='{url}'");

        if (OnToast != null)
        {
            try
            {
                await OnToast.Invoke(message, type, url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ToastService] ERROR: {ex.Message}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ToastService] WARNING: No subscribers!");
        }
    }

    /// <summary>
    /// Show success toast (green)
    /// </summary>
    public async Task ShowSuccessAsync(string message, string? url = null)
        => await ShowToastAsync(message, "success", url);

    /// <summary>
    /// Show error toast (red)
    /// </summary>
    public async Task ShowErrorAsync(string message, string? url = null)
        => await ShowToastAsync(message, "error", url);

    /// <summary>
    /// Show warning toast (orange)
    /// </summary>
    public async Task ShowWarningAsync(string message, string? url = null)
        => await ShowToastAsync(message, "warning", url);

    /// <summary>
    /// Show info toast (blue)
    /// </summary>
    public async Task ShowInfoAsync(string message, string? url = null)
        => await ShowToastAsync(message, "info", url);
}
