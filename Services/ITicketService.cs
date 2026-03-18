using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface ITicketService
    {
        Task<QueueTicket> GenerateTicketAsync(string patientName, string patientPhone, int departmentId);
        Task<Patient?> FindPatientByPhoneAsync(string phone);
    }
}
