using Microsoft.EntityFrameworkCore;
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

            var nextTicketNumber = await _context.Database
                .SqlQueryRaw<int>(@"SELECT nextval('""TicketNumberSeq""') AS ""Value""")
                .SingleAsync();

            var ticket = new QueueTicket
            {
                PatientId = newPatient.Id,
                TicketNumber = nextTicketNumber,
                Status = "Waiting",
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            };

            _context.QueueTickets.Add(ticket);
            await _context.SaveChangesAsync();

            return ticket;
        }
    }
}
