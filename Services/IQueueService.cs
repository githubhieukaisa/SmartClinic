using SmartClinic.DTOs;

namespace SmartClinic.Services
{
    public interface IQueueService
    {
        Task<QueueDisplayDto> GetDisplayDataAsync(int roomId);
        Task<bool> CallNextPatientAsync(int roomId);
    }
}
