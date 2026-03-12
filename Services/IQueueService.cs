using SmartClinic.DTOs;

namespace SmartClinic.Services
{
    public interface IQueueService
    {
        Task<QueueDisplayDto> GetDisplayDataAsync(int RoomId);
        Task<bool> CallNextPatientAsync(string roomName);
    }
}
