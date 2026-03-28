using Microsoft.AspNetCore.SignalR;
using SmartClinic.Hubs;
using Microsoft.Extensions.Logging;

namespace SmartClinic.Services;

public class WeeklyScheduleReminderJob
{
    private readonly IHubContext<PatientHub> _hubContext;
    private readonly ILogger<WeeklyScheduleReminderJob> _logger;

    public WeeklyScheduleReminderJob(IHubContext<PatientHub> hubContext, ILogger<WeeklyScheduleReminderJob> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Hangfire: Bắt đầu gửi thông báo nhắc nhở phân lịch tuần tới...");
        
        try
        {
            // Push broadcast message to all active admin/manager sessions
            await _hubContext.Clients.All.SendAsync("SystemNotification", "Đã đến cuối tuần, vui lòng phân lịch trực cho bác sĩ tuần sau!");
            
            _logger.LogInformation("Hangfire: Đã gửi thông báo nhắc nhở thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi thông báo nhắc nhở qua Hangfire!");
            throw; // Hangfire sẽ retry nếu bị ném lỗi
        }
    }
}
