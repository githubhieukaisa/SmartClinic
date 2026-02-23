using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class TicketService : ITicketService
    {
        private readonly SmartClinicDbContext _context;

        public TicketService(SmartClinicDbContext context)
        {
            _context = context;
        }

        public async Task<QueueTicket> GenerateTicketAsync(Patient newPatient)
        {
            _context.Patients.Add(newPatient);
            await _context.SaveChangesAsync();

            var today = DateTime.Today;
            var maxTicketNum = _context.QueueTickets
                .Where(t => t.CreatedAt.Value.Date == today)
                .Max(t => (int?)t.TicketNumber) ?? 0;

            var newTicketNum = maxTicketNum + 1;

            var ticket = new QueueTicket
            {
                PatientId = newPatient.Id,
                TicketNumber = newTicketNum,
                Status = "Waiting",
                CreatedAt = DateTime.Now
            };

            _context.QueueTickets.Add(ticket);
            await _context.SaveChangesAsync();

            return ticket;
        }
    }
}
