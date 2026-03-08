using SmartClinic.DTOs;

namespace SmartClinic.Services
{
    public interface IQueueService
    {
        Task<QueueDisplayDto> GetDisplayDataAsync();
        Task<bool> CallNextPatientAsync(string roomName);
    }
}
