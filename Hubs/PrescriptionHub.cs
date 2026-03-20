using Microsoft.AspNetCore.SignalR;

namespace SmartClinic.Hubs
{
    /// <summary>
    /// Hub xử lý real-time notifications cho Dược sĩ và Thu ngân
    /// Group conventions:
    ///   "Pharmacists" - tất cả dược sĩ đang online
    ///   "Cashiers"    - tất cả thu ngân đang online
    /// </summary>
    public class PrescriptionHub : Hub
    {
        public async Task JoinPharmacistGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Pharmacists");
            System.Diagnostics.Debug.WriteLine($"[PrescriptionHub] {Context.ConnectionId} joined Pharmacists");
        }

        public async Task JoinCashierGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Cashiers");
            System.Diagnostics.Debug.WriteLine($"[PrescriptionHub] {Context.ConnectionId} joined Cashiers");
        }

        public async Task LeavePharmacistGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Pharmacists");
        }

        public async Task LeaveCashierGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Cashiers");
        }
    }
}