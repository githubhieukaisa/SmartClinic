using Microsoft.AspNetCore.SignalR;
namespace SmartClinic.Hubs
{
    public class PatientHub : Hub
    {
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
    }
}

