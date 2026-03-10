using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    /// <summary>
    /// Patient and Queue Management Service
    /// 
    /// Refactored to use IDbContextFactory<SmartClinicDbContext> to prevent
    /// ObjectDisposedException when called from SignalR callbacks.
    /// 
    /// Key changes:
    /// - Each method creates its own DbContext instance using the factory
    /// - Context is properly disposed via 'await using' statement
    /// - Safe to call from SignalR events and async operations
    /// - No dependency on scoped DbContext lifetime
    /// </summary>
    public class PatientService
    {
        private readonly IDbContextFactory<SmartClinicDbContext> _contextFactory;
        private readonly IHubContext<PatientHub> _hubContext;

        public PatientService(
            IDbContextFactory<SmartClinicDbContext> contextFactory,
            IHubContext<PatientHub> hubContext)
        {
            _contextFactory = contextFactory;
            _hubContext = hubContext;
            System.Diagnostics.Debug.WriteLine("✅ [PatientService] Initialized with DbContextFactory");
        }

        /// <summary>
        /// Add a new patient to the database
        /// Creates its own DbContext instance
        /// </summary>
        public async Task AddPatientAsync(Patient patient)
        {
            System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.AddPatientAsync] Adding patient: {patient.FullName}");
            
            // Create a fresh context for this operation
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            try
            {
                context.Patients.Add(patient);
                await context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"✅ [PatientService.AddPatientAsync] Patient saved with ID={patient.Id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService.AddPatientAsync] ERROR: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get all active patients
        /// Creates its own DbContext instance
        /// </summary>
        public async Task<List<Patient>> GetActivePatientsAsync()
        {
            System.Diagnostics.Debug.WriteLine("🔵 [PatientService.GetActivePatientsAsync] Fetching all patients");
            
            // Create a fresh context for this operation
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            try
            {
                var patients = await context.Patients
                    .AsNoTracking()
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();
                
                System.Diagnostics.Debug.WriteLine($"✅ [PatientService.GetActivePatientsAsync] Found {patients.Count} patients");
                return patients;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService.GetActivePatientsAsync] ERROR: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get a single patient by ID
        /// Creates its own DbContext instance
        /// Safe to call from SignalR callbacks
        /// </summary>
        public async Task<Patient?> GetPatientByIdAsync(int patientId)
        {
            System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.GetPatientByIdAsync] Fetching patient ID={patientId}");
            
            // Create a fresh context for this operation
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            try
            {
                var patient = await context.Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == patientId);
                
                if (patient != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ [PatientService.GetPatientByIdAsync] Found patient: {patient.FullName}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ [PatientService.GetPatientByIdAsync] Patient not found");
                }
                
                return patient;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService.GetPatientByIdAsync] ERROR: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get doctor's queue with status "Waiting" or "Examining"
        /// Creates its own DbContext instance
        /// Safe to call from SignalR callbacks
        /// </summary>
        public async Task<List<QueueTicket>> GetDoctorQueueAsync(int doctorId)
        {
            System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.GetDoctorQueueAsync] Fetching queue for DoctorId={doctorId}");
            
            // ✅ Create a fresh context for this operation
            // This prevents ObjectDisposedException when called from SignalR
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            try
            {
                var tickets = await context.QueueTickets
                    .AsNoTracking()  // No tracking needed for read-only queries
                    .Include(t => t.Patient)
                    .Where(t => t.DoctorId == doctorId && (t.Status == "Waiting" || t.Status == "Examining"))
                    .OrderBy(t => t.CreatedAt)
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"✅ [PatientService.GetDoctorQueueAsync] Found {tickets.Count} tickets");
                
                foreach (var t in tickets)
                {
                    System.Diagnostics.Debug.WriteLine($"   - Ticket #{t.TicketNumber}: {t.Patient?.FullName} (Status: {t.Status})");
                }
                
                return tickets;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService.GetDoctorQueueAsync] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService.GetDoctorQueueAsync] Stack: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Create a QueueTicket for a patient and notify via SignalR
        /// Creates its own DbContext instance
        /// Safe to call from SignalR callbacks
        /// </summary>
        public async Task AddQueueTicketAsync(int doctorId, int patientId, string? diagnosis = null)
        {
            System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.AddQueueTicketAsync] Creating ticket for DoctorId={doctorId}, PatientId={patientId}");
            
            // ✅ Create a fresh context for this operation
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            try
            {
                // Get the next ticket number for this doctor
                var lastTicket = await context.QueueTickets
                    .AsNoTracking()
                    .Where(t => t.DoctorId == doctorId)
                    .OrderByDescending(t => t.TicketNumber)
                    .FirstOrDefaultAsync();

                var nextTicketNumber = (lastTicket?.TicketNumber ?? 0) + 1;
                System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.AddQueueTicketAsync] Next ticket number: {nextTicketNumber}");

                var queueTicket = new QueueTicket
                {
                    DoctorId = doctorId,
                    PatientId = patientId,
                    TicketNumber = nextTicketNumber,
                    Status = "Waiting",
                    ClinicalDiagnosis = diagnosis,
                    CreatedAt = DateTime.Now
                };

                context.QueueTickets.Add(queueTicket);
                await context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"✅ [PatientService.AddQueueTicketAsync] Ticket #{nextTicketNumber} created with ID={queueTicket.Id}");

                // Broadcast SignalR notification
                System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.AddQueueTicketAsync] Broadcasting QueueTicketUpdated event");
                await _hubContext.Clients.All.SendAsync("QueueTicketUpdated", new 
                { 
                    doctorId, 
                    ticketId = queueTicket.Id 
                });
                System.Diagnostics.Debug.WriteLine($"✅ [PatientService.AddQueueTicketAsync] SignalR event sent");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService.AddQueueTicketAsync] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService.AddQueueTicketAsync] Stack: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Update QueueTicket status and notify via SignalR
        /// Creates its own DbContext instance
        /// Safe to call from SignalR callbacks
        /// </summary>
        public async Task UpdateQueueTicketStatusAsync(int ticketId, string newStatus, int doctorId)
        {
            System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.UpdateQueueTicketStatusAsync] Updating TicketId={ticketId} to Status={newStatus}");

            // ✅ Create a fresh context for this operation
            // Critical for SignalR callbacks - prevents disposed context errors
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            try
            {
                // Find the ticket
                var ticket = await context.QueueTickets.FindAsync(ticketId);
                if (ticket == null)
                {
                    throw new InvalidOperationException($"QueueTicket with ID {ticketId} not found");
                }

                string oldStatus = ticket.Status;
                System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.UpdateQueueTicketStatusAsync] Current status: {oldStatus}");

                // Update status
                ticket.Status = newStatus;
                await context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"✅ [PatientService.UpdateQueueTicketStatusAsync] Status updated: {oldStatus} → {newStatus}");

                // Broadcast SignalR notification
                System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.UpdateQueueTicketStatusAsync] Broadcasting QueueStatusUpdated event");
                await _hubContext.Clients.All.SendAsync("QueueStatusUpdated", new
                {
                    doctorId,
                    ticketId,
                    oldStatus,
                    newStatus,
                    patientId = ticket.PatientId,
                    timestamp = DateTime.Now
                });
                System.Diagnostics.Debug.WriteLine($"✅ [PatientService.UpdateQueueTicketStatusAsync] SignalR event sent");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService.UpdateQueueTicketStatusAsync] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService.UpdateQueueTicketStatusAsync] Stack: {ex.StackTrace}");
                throw;
            }
        }
    }
}

