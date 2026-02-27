using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class PatientService 
    {
        private readonly SmartClinicDbContext _context;
        private readonly IHubContext<PatientHub> _hubContext;

        public PatientService(SmartClinicDbContext context, IHubContext<PatientHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task AddPatientAsync(Patient patient)
        {
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            // Bắn sự kiện "PatientListUpdated" tới tất cả các client đang kết nối
            await _hubContext.Clients.All.SendAsync("PatientListUpdated");
        }

        public async Task<List<Patient>> GetActivePatientsAsync()
        {
            return await _context.Patients
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
    }
}
