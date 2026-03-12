using Microsoft.AspNetCore.SignalR;

namespace SmartClinic.Hubs
{
    public class QueueHub : Hub
    {
        public async Task JoinRoomGroup(int roomId)
        {
            string groupName = $"Room_{roomId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
    }
}
