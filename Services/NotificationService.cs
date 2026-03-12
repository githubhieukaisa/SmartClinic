using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace SmartClinic.Services;

/// <summary>
/// SignalR Notification Service - Lazy Initialization Pattern
/// 
/// Registered as SINGLETON - no dependencies on scoped services.
/// Uses relative URL for HubConnection, which works in Blazor Server.
/// 
/// Architecture:
/// - Singleton registration ensures one connection per session
/// - Lazy initialization avoids startup race conditions
/// - Relative URL allows Blazor to resolve the connection automatically
/// - Pages call EnsureStartedAsync() during their initialization
/// - Connection is reused across all pages
/// - WithAutomaticReconnect() handles disconnections gracefully
/// </summary>
public class NotificationService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private bool _isConnecting = false;
    private TaskCompletionSource<bool>? _connectionTask;
    private readonly object _connectionLock = new();

    // SignalR Hub endpoint URL (relative - Blazor will resolve it)
    private const string HubUrl = "https://localhost:7062/hubs/patient";

    // Events for queue updates
    public event Action? OnPatientQueueUpdated;
    public event Action<int>? OnQueueStatusUpdated;

    public NotificationService()
    {
        System.Diagnostics.Debug.WriteLine($"");
        System.Diagnostics.Debug.WriteLine($"[NotificationService] ===== INITIALIZED =====");
        System.Diagnostics.Debug.WriteLine($"[NotificationService] Ready to handle SignalR events");
        System.Diagnostics.Debug.WriteLine($"");
    }

    /// <summary>
    /// Connection status property
    /// Returns true only if connection is currently connected
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Get current connection state for debugging
    /// </summary>
    public HubConnectionState? ConnectionState => _hubConnection?.State;

    /// <summary>
    /// Ensure SignalR connection is started
    /// 
    /// Safe to call from multiple pages simultaneously.
    /// - First call: Creates connection and starts it
    /// - Subsequent calls: Returns immediately if already connected
    ///                     or waits if currently connecting
    /// 
    /// This method:
    /// 1. Checks if connection already exists and is connected
    /// 2. If already connecting, waits for that to complete
    /// 3. If not started, builds and starts the connection
    /// 4. Handles race conditions with TaskCompletionSource
    /// </summary>
    public async Task EnsureStartedAsync()
    {
        System.Diagnostics.Debug.WriteLine("🔵 [NotificationService.EnsureStartedAsync] Checking connection status");

        // Fast path: If already connected, return immediately
        if (IsConnected)
        {
            System.Diagnostics.Debug.WriteLine("✅ [NotificationService.EnsureStartedAsync] Already connected");
            return;
        }

        // If currently connecting, wait for that to complete
        if (_isConnecting && _connectionTask != null)
        {
            System.Diagnostics.Debug.WriteLine("⏳ [NotificationService.EnsureStartedAsync] Waiting for ongoing connection attempt");
            try
            {
                await _connectionTask.Task;
                System.Diagnostics.Debug.WriteLine("✅ [NotificationService.EnsureStartedAsync] Connection completed");
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ [NotificationService.EnsureStartedAsync] Connection failed: {ex.Message}");
                throw;
            }
        }

        // Start new connection attempt
        await StartConnectionAsync();
    }

    /// <summary>
    /// Internal method to start the SignalR connection
    /// Handles all connection logic and event registration
    /// 
    /// Uses relative URL (/hubs/patient) which Blazor resolves automatically
    /// No NavigationManager dependency needed
    /// </summary>
    private async Task StartConnectionAsync()
    {
        // Thread-safe check-and-set
        lock (_connectionLock)
        {
            if (_isConnecting)
            {
                System.Diagnostics.Debug.WriteLine("⏳ [NotificationService.StartConnectionAsync] Connection already in progress");
                return;
            }

            _isConnecting = true;
            _connectionTask = new TaskCompletionSource<bool>();
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("🔵 [NotificationService.StartConnectionAsync] Starting connection");

            // Build the HubConnection using relative URL
            // Blazor Server automatically resolves relative URLs from the client context
            System.Diagnostics.Debug.WriteLine($"🔵 [NotificationService.StartConnectionAsync] Building HubConnection to {HubUrl}");
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(HubUrl)  // ✅ Relative URL - no NavigationManager needed
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .WithServerTimeout(TimeSpan.FromSeconds(30))
                .Build();

            // Register event handlers BEFORE connecting
            RegisterEventHandlers();

            // Start the connection
            System.Diagnostics.Debug.WriteLine("🔵 [NotificationService.StartConnectionAsync] Calling HubConnection.StartAsync()");
            await _hubConnection.StartAsync();

            System.Diagnostics.Debug.WriteLine($"✅ [NotificationService.StartConnectionAsync] Connected! State={_hubConnection.State}");
            _connectionTask?.SetResult(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [NotificationService.StartConnectionAsync] Connection failed");
            System.Diagnostics.Debug.WriteLine($"❌ Exception: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");

            // Cleanup
            if (_hubConnection != null)
            {
                try
                {
                    await _hubConnection.DisposeAsync();
                }
                catch { }
                _hubConnection = null;
            }

            _connectionTask?.SetException(ex);
            throw;
        }
        finally
        {
            lock (_connectionLock)
            {
                _isConnecting = false;
            }
        }
    }

    /// <summary>
    /// Register all SignalR event handlers
    /// Called after HubConnection is built, before StartAsync()
    /// </summary>
    private void RegisterEventHandlers()
    {
        if (_hubConnection == null)
        {
            System.Diagnostics.Debug.WriteLine("⚠️ [NotificationService.RegisterEventHandlers] HubConnection is null");
            return;
        }

        // Handle QueueTicketUpdated event - when a new patient is added to queue
        _hubConnection.On("QueueTicketUpdated", (JsonElement data) =>
        {
            System.Diagnostics.Debug.WriteLine("🔵 [NotificationService] Received QueueTicketUpdated event");

            try
            {
                int doctorId = data.GetProperty("doctorId").GetInt32();
                int ticketId = data.GetProperty("ticketId").GetInt32();

                System.Diagnostics.Debug.WriteLine($"🔵 [NotificationService] Event: DoctorId={doctorId}, TicketId={ticketId}");

                // Only notify if for current doctor (doctorId = 1 in this case)
                if (doctorId == 1)
                {
                    System.Diagnostics.Debug.WriteLine("[SignalR] QueueTicketUpdated received");
                    System.Diagnostics.Debug.WriteLine("✅ [NotificationService] Invoking OnPatientQueueUpdated");
                    OnPatientQueueUpdated?.Invoke();
                    
                    // Note: Toast notification is now shown from the page/component level
                    // using ToastNotificationService directly
                    System.Diagnostics.Debug.WriteLine("[NotificationService] Toast notification will be shown by the page");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [NotificationService] Error in QueueTicketUpdated handler: {ex.Message}");
            }
        });

        // Handle QueueStatusUpdated event - when ticket status changes
        _hubConnection.On("QueueStatusUpdated", (JsonElement data) =>
        {
            System.Diagnostics.Debug.WriteLine("🔵 [NotificationService] Received QueueStatusUpdated event");

            try
            {
                int ticketId = data.GetProperty("ticketId").GetInt32();
                int doctorId = data.GetProperty("doctorId").GetInt32();
                string newStatus = data.GetProperty("newStatus").GetString() ?? "Unknown";

                System.Diagnostics.Debug.WriteLine($"🔵 [NotificationService] Event: TicketId={ticketId}, Status={newStatus}");

                OnQueueStatusUpdated?.Invoke(ticketId);
                System.Diagnostics.Debug.WriteLine("✅ [NotificationService] Invoked OnQueueStatusUpdated");
                
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Toast notification will be shown by the page");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [NotificationService] Error in QueueStatusUpdated handler: {ex.Message}");
            }
        });

        // Handle reconnection event
        _hubConnection.Reconnecting += (error) =>
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ [NotificationService] Reconnecting... Error: {error?.Message}");
            return Task.CompletedTask;
        };

        // Handle reconnected event
        _hubConnection.Reconnected += (connectionId) =>
        {
            System.Diagnostics.Debug.WriteLine($"✅ [NotificationService] Reconnected with ConnectionId={connectionId}");
            return Task.CompletedTask;
        };

        // Handle closed event
        _hubConnection.Closed += (error) =>
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ [NotificationService] Connection closed. Error: {error?.Message}");
            return Task.CompletedTask;
        };

        System.Diagnostics.Debug.WriteLine("✅ [NotificationService.RegisterEventHandlers] All handlers registered");
    }

    /// <summary>
    /// Dispose the connection gracefully
    /// Called when the application shuts down
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        System.Diagnostics.Debug.WriteLine("🔵 [NotificationService.DisposeAsync] Disposing connection");

        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.DisposeAsync();
                System.Diagnostics.Debug.WriteLine("✅ [NotificationService.DisposeAsync] Connection disposed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ [NotificationService.DisposeAsync] Error during disposal: {ex.Message}");
            }
        }
    }
}

