using SmartClinic.Constant;
using SmartClinic.DTOs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface ITicketService
    {
        Task<QueueTicket> GenerateTicketAsync(GenerateTicketRequest request);
        Task<QueueTicket> GenerateTicketAsync(string patientName, string patientPhone, int departmentId, int? userId = null);
        Task<User?> FindPatientByPhoneAsync(string phone);
    }
}
