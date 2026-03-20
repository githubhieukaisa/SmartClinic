using Microsoft.AspNetCore.SignalR.Client;
using SmartClinic.DTOs;
using System.Text.Json;

namespace SmartClinic.Services
{
    /// <summary>
    /// Singleton SignalR client for pharmacy/cashier real-time notifications.
    ///
    /// Mirrors the pattern of NotificationService but connects to /hubs/prescription.
    /// Exposes:
    ///   - OnNewPrescription   → fires when doctor saves a prescription
    ///   - OnPrescriptionPaid  → fires when cashier records payment (optional refresh trigger)
    /// </summary>
    public class PharmacistNotificationService : IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private bool _isConnecting;
        private TaskCompletionSource<bool>? _connectionTask;
        private readonly object _lock = new();

        private const string HubUrl = "https://localhost:7062/hubs/prescription";

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<NewPrescriptionNotificationDto>? OnNewPrescription;
        public event Action<PrescriptionDispensedNotificationDto>? OnPrescriptionDispensed;

        // ── State ─────────────────────────────────────────────────────────────
        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public HubConnectionState? ConnectionState => _hubConnection?.State;

        // ── Public API ────────────────────────────────────────────────────────

        public async Task EnsureStartedAsync()
        {
            if (IsConnected) return;

            if (_isConnecting && _connectionTask != null)
            {
                await _connectionTask.Task;
                return;
            }

            await StartConnectionAsync();
        }

        public async Task JoinPharmacistGroupAsync()
        {
            await EnsureStartedAsync();
            await _hubConnection!.InvokeAsync("JoinPharmacistGroup");
        }

        public async Task JoinCashierGroupAsync()
        {
            await EnsureStartedAsync();
            await _hubConnection!.InvokeAsync("JoinCashierGroup");
        }

        // ── Private ───────────────────────────────────────────────────────────

        private async Task StartConnectionAsync()
        {
            lock (_lock)
            {
                if (_isConnecting) return;
                _isConnecting = true;
                _connectionTask = new TaskCompletionSource<bool>();
            }

            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(HubUrl)
                    .WithAutomaticReconnect(new[]
                    {
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(30)
                    })
                    .Build();

                RegisterHandlers();
                await _hubConnection.StartAsync();
                _connectionTask?.SetResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PharmacistNotificationService] Connection failed: {ex.Message}");

                if (_hubConnection != null)
                {
                    try { await _hubConnection.DisposeAsync(); } catch { }
                    _hubConnection = null;
                }

                _connectionTask?.SetException(ex);
                throw;
            }
            finally
            {
                lock (_lock) { _isConnecting = false; }
            }
        }

        private void RegisterHandlers()
        {
            if (_hubConnection == null) return;

            _hubConnection.On<JsonElement>("NewPrescriptionReady", data =>
            {
                try
                {
                    var dto = new NewPrescriptionNotificationDto
                    {
                        PrescriptionId = data.GetProperty("prescriptionId").GetInt32(),
                        TicketId = data.GetProperty("ticketId").GetInt32(),
                        PatientName = data.GetProperty("patientName").GetString() ?? "",
                        DoctorName = data.GetProperty("doctorName").GetString() ?? "",
                        MedicineCount = data.GetProperty("medicineCount").GetInt32(),
                        TotalAmount = data.GetProperty("totalAmount").GetDecimal(),
                        CreatedAt = data.GetProperty("createdAt").GetDateTime()
                    };
                    OnNewPrescription?.Invoke(dto);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PharmacistNotificationService] NewPrescriptionReady parse error: {ex.Message}");
                }
            });

            _hubConnection.On<JsonElement>("PrescriptionDispensed", data =>
            {
                try
                {
                    var dto = new PrescriptionDispensedNotificationDto
                    {
                        PrescriptionId = data.GetProperty("prescriptionId").GetInt32(),
                        TicketId = data.GetProperty("ticketId").GetInt32(),
                        PatientName = data.GetProperty("patientName").GetString() ?? "",
                        TotalAmount = data.GetProperty("totalAmount").GetDecimal()
                    };
                    OnPrescriptionDispensed?.Invoke(dto);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PharmacistNotificationService] PrescriptionDispensed parse error: {ex.Message}");
                }
            });
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                try { await _hubConnection.DisposeAsync(); } catch { }
            }
        }
    }
}