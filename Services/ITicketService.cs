using SmartClinic.DTOs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface ITicketService
    {
        Task<List<ReceptionRoomLiveItemDto>> GetAvailableShiftsAsync(int departmentId);
        Task<QueueTicket> GenerateTicketAsync(GenerateTicketRequest request);
        Task<QueueTicket> GenerateTicketAsync(string patientName, string? patientPhone, int departmentId, int? userId = null);
        Task<User?> FindPatientByPhoneAsync(string phone);
        Task<ReceptionDashboardDto> GetReceptionDashboardAsync(string? keyword = null);
        Task<AppointmentCheckInResultDto> ConfirmAppointmentCheckInAsync(int ticketId, int? receptionistUserId = null);
    }
}
