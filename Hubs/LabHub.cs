using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SmartClinic.Hubs;

public class LabHub : Hub
{
    // Gọi bởi màn hình kỹ thuật viên xét nghiệm
    public async Task JoinLabTechGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "LabTechnicians");
        System.Diagnostics.Debug.WriteLine($"✅ [LabHub] Client {Context.ConnectionId} joined LabTechnicians");
    }

    // Gọi bởi màn hình phòng khám của bác sĩ để nhận kết quả
    public async Task JoinDoctorRoom(int roomId)
    {
        string groupName = $"DoctorRoom_{roomId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        System.Diagnostics.Debug.WriteLine($"✅ [LabHub] Client {Context.ConnectionId} joined {groupName}");
    }
}
