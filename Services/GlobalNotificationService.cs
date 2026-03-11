using System.Collections.Concurrent;

namespace SmartClinic.Services;

/// <summary>
/// Global Notification Service - State Store for UI Notifications
/// 
/// Registered as SINGLETON - acts as a centralized notification store.
/// 
/// Architecture:
/// - Singleton: Persists notifications across component lifecycle
/// - State Store: Components read notification state directly
/// - Thread-Safe: Uses ConcurrentQueue for background thread safety
/// - Event Optional: Events support reactive updates but aren't required
/// 
/// Key Principle:
/// NotificationContainer reads notifications directly from service state,
/// rather than relying on fragile event subscriptions that break during
/// component dispose/recreate cycles.
/// </summary>
public class GlobalNotificationService
{
    /// <summary>
    /// Represents a single notification
    /// </summary>
    public class Notification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "info"; // info, success, warning, error
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Internal notification store - thread-safe queue
    /// </summary>
    private readonly ConcurrentQueue<Notification> _notifications = new();

    /// <summary>
    /// OPTIONAL: Event fired when a new notification is added
    /// Components should NOT rely on this for notification delivery.
    /// Instead, read notifications directly via GetNotifications().
    /// </summary>
    public event Action? OnNotificationAdded;

    /// <summary>
    /// OPTIONAL: Event fired when a notification is removed
    /// Components should NOT rely on this for notification delivery.
    /// Instead, read notifications directly via GetNotifications().
    /// </summary>
    public event Action? OnNotificationRemoved;

    public GlobalNotificationService()
    {
        System.Diagnostics.Debug.WriteLine($"");
        System.Diagnostics.Debug.WriteLine($"[GlobalNotificationService] ===== SINGLETON INITIALIZED =====");
        System.Diagnostics.Debug.WriteLine($"[GlobalNotificationService] Instance HashCode: {this.GetHashCode()}");
        System.Diagnostics.Debug.WriteLine($"[GlobalNotificationService] This instance will be shared across entire app");
        System.Diagnostics.Debug.WriteLine($"");
    }

    /// <summary>
    /// Get current list of active notifications from state store
    /// 
    /// IMPORTANT: Components should call this directly instead of relying on events.
    /// This ensures notifications are never lost, even during component recreation.
    /// 
    /// Returns: IReadOnlyList of current notifications (safe copy)
    /// </summary>
    public IReadOnlyList<Notification> GetNotifications()
    {
        return _notifications.ToList().AsReadOnly();
    }

    /// <summary>
    /// Get notification count from state store
    /// </summary>
    public int NotificationCount => _notifications.Count;

    /// <summary>
    /// Show a notification with optional type
    /// Thread-safe - can be called from background threads
    /// 
    /// Adds notification to STATE STORE. NotificationContainer will read from state,
    /// not from events, so this works even if component is recreated.
    /// 
    /// Parameters:
    /// - message: The notification message
    /// - type: "info" (default), "success", "warning", or "error"
    /// </summary>
    public void Show(string message, string type = "info")
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var notification = new Notification
        {
            Message = message,
            Type = type
        };

        // Add to state store
        _notifications.Enqueue(notification);
        
        System.Diagnostics.Debug.WriteLine($"");
        System.Diagnostics.Debug.WriteLine($"[GlobalNotificationService.Show] NOTIFICATION ADDED");
        System.Diagnostics.Debug.WriteLine($"[GlobalNotificationService.Show] Instance HashCode: {this.GetHashCode()}");
        System.Diagnostics.Debug.WriteLine($"[GlobalNotificationService.Show] Message: {message}");
        System.Diagnostics.Debug.WriteLine($"[GlobalNotificationService.Show] Type: {type}");
        System.Diagnostics.Debug.WriteLine($"[GlobalNotificationService.Show] Queue Size: {_notifications.Count}");
        System.Diagnostics.Debug.WriteLine($"");

        // Fire optional event for reactive updates (not required for delivery)
        try
        {
            OnNotificationAdded?.Invoke();
            System.Diagnostics.Debug.WriteLine($"[GlobalNotificationService.Show] OnNotificationAdded event fired");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ [GlobalNotificationService.Show] Event invoke error (ignoring): {ex.Message}");
        }
    }

    /// <summary>
    /// Remove a specific notification by ID from STATE STORE
    /// Called when notification times out or user dismisses it
    /// </summary>
    public void Remove(string notificationId)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification != null)
        {
            // Rebuild queue without the notification
            var remaining = _notifications.Where(n => n.Id != notificationId).ToList();
            while (_notifications.TryDequeue(out _)) { }
            foreach (var n in remaining)
                _notifications.Enqueue(n);

            System.Diagnostics.Debug.WriteLine($"🗑️ [GlobalNotificationService.Remove] Removed from state: {notificationId}");
            System.Diagnostics.Debug.WriteLine($"   Remaining in queue: {_notifications.Count}");
            
            // Fire optional event
            try
            {
                OnNotificationRemoved?.Invoke();
            }
            catch { }
        }
    }

    /// <summary>
    /// Clear all notifications from STATE STORE
    /// </summary>
    public void ClearAll()
    {
        while (_notifications.TryDequeue(out _)) { }
        System.Diagnostics.Debug.WriteLine($"🗑️ [GlobalNotificationService.ClearAll] Cleared all notifications");
        
        // Fire optional event
        try
        {
            OnNotificationRemoved?.Invoke();
        }
        catch { }
    }
}
