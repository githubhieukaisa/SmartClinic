using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface ITicketService
    {
        Task<QueueTicket> GenerateTicketAsync(Patient newPatient, int departmentId);
    }
}
