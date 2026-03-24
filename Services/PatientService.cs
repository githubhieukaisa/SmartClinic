using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
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
        }

        /// <summary>
        /// Add a new patient to the database
        /// Creates its own DbContext instance
        /// </summary>
        public async Task AddPatientAsync(Patient patient)
        {
            // Create a fresh context for this operation
            await using var context = await _contextFactory.CreateDbContextAsync();

            try
            {
                context.Patients.Add(patient);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService] AddPatientAsync ERROR: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get all active patients
        /// Creates its own DbContext instance
        /// </summary>
        public async Task<List<Patient>> GetActivePatientsAsync()
        {
            // Create a fresh context for this operation
            await using var context = await _contextFactory.CreateDbContextAsync();

            try
            {
                var patients = await context.Patients
                    .AsNoTracking()
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                return patients;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService] GetActivePatientsAsync ERROR: {ex.Message}");
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
            // Create a fresh context for this operation
            await using var context = await _contextFactory.CreateDbContextAsync();

            try
            {
                var patient = await context.Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                return patient;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService] GetPatientByIdAsync ERROR: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get doctor's queue based on DoctorShift room assignment
        /// Queries QueueTickets where RoomId matches the doctor's current shift room
        /// Creates its own DbContext instance
        /// Safe to call from SignalR callbacks
        /// </summary>
        public async Task<List<QueueTicket>> GetDoctorQueueAsync(int doctorId)
        {
            // Create a fresh context for this operation
            // This prevents ObjectDisposedException when called from SignalR
            await using var context = await _contextFactory.CreateDbContextAsync();

            try
            {
                // STEP 1: Get doctor's current active shift and its room id
                var doctorShift = await context.DoctorShifts
                    .AsNoTracking()
                    .Where(ds => ds.DoctorId == doctorId && ds.StatusEnum == DoctorShiftStatus.Active)
                    .OrderByDescending(ds => ds.StartTime)
                    .FirstOrDefaultAsync();

                // If no active shift found, return empty list
                if (doctorShift == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ [PatientService] No active DoctorShift found for DoctorId={doctorId}");
                    return new List<QueueTicket>();
                }

                System.Diagnostics.Debug.WriteLine($"🔵 [PatientService] Found active DoctorShift: RoomId={doctorShift.RoomId}");

                // STEP 2: Get all queue tickets for this room (not just this doctor)
                // This ensures all patients in the room are visible to the doctor
                var tickets = await context.QueueTickets
                    .AsNoTracking()  // No tracking needed for read-only queries
                    .Include(t => t.Patient)
                    .Where(t => t.RoomId == doctorShift.RoomId && (t.StatusEnum == TicketStatus.Waiting || t.StatusEnum == TicketStatus.Examinating || t.StatusEnum == TicketStatus.Calling || t.StatusEnum == TicketStatus.Testing))
                    .ToListAsync();  // Load to memory first, then sort

                // Sort by priority: Examining → Testing → Calling → Waiting
                var sortedTickets = tickets
                    .OrderBy(t => t.StatusEnum == TicketStatus.Examinating ? 0 : t.StatusEnum == TicketStatus.Testing ? 1 : t.StatusEnum == TicketStatus.Calling ? 2 : 3)
                    .ThenByDescending(t => t.CreatedAt)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"✅ [PatientService] Found {sortedTickets.Count} queue tickets for RoomId={doctorShift.RoomId}");
                return sortedTickets;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PatientService] GetDoctorQueueAsync ERROR: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create a QueueTicket for a patient and notify via SignalR
        /// Automatically assigns the ticket to the doctor's current shift room
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
                // STEP 1: Get doctor's current active shift to find the room
                var doctorShift = await context.DoctorShifts
                    .AsNoTracking()
                    .Where(ds => ds.DoctorId == doctorId && ds.StatusEnum == DoctorShiftStatus.Active)
                    .OrderByDescending(ds => ds.StartTime)
                    .FirstOrDefaultAsync();

                if (doctorShift == null)
                {
                    throw new InvalidOperationException($"No active DoctorShift found for DoctorId={doctorId}. Cannot assign room.");
                }

                System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.AddQueueTicketAsync] Found active shift with RoomId={doctorShift.RoomId}");

                // STEP 2: Get the next ticket number for this doctor
                var lastTicket = await context.QueueTickets
                    .AsNoTracking()
                    .Where(t => t.DoctorId == doctorId)
                    .OrderByDescending(t => t.TicketNumber)
                    .FirstOrDefaultAsync();

                var nextTicketNumber = (lastTicket?.TicketNumber ?? 0) + 1;
                System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.AddQueueTicketAsync] Next ticket number: {nextTicketNumber}");

                // STEP 3: Create queue ticket with room from doctor's shift
                var queueTicket = new QueueTicket
                {
                    DoctorId = doctorId,
                    PatientId = patientId,
                    TicketNumber = nextTicketNumber,
                    StatusEnum = TicketStatus.Waiting,
                    ClinicalDiagnosis = diagnosis,
                    CreatedAt = DateTime.Now,
                    RoomId = doctorShift.RoomId  // ✅ Get room from active doctor shift
                };

                context.QueueTickets.Add(queueTicket);
                await context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"✅ [PatientService.AddQueueTicketAsync] Ticket #{nextTicketNumber} created with ID={queueTicket.Id}, RoomId={doctorShift.RoomId}");

                var patient = await context.Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                string patientName = patient?.FullName ?? "Unknown";
                // Broadcast SignalR notification to the specific room group only
                System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.AddQueueTicketAsync] Broadcasting QueueTicketUpdated event to Room_{doctorShift.RoomId}");
                await _hubContext.Clients.Group($"Room_{doctorShift.RoomId}").SendAsync("QueueTicketUpdated", new
                {
                    doctorId,
                    ticketId = queueTicket.Id,
                    patientName,
                    roomId = doctorShift.RoomId
                });
                System.Diagnostics.Debug.WriteLine($"✅ [PatientService.AddQueueTicketAsync] SignalR event sent to Room_{doctorShift.RoomId}");
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

                var oldStatus = ticket.StatusEnum;
                System.Diagnostics.Debug.WriteLine($"🔵 [PatientService.UpdateQueueTicketStatusAsync] Current status: {oldStatus}");

                // Update status
                ticket.StatusEnum = Enum.Parse<TicketStatus>(newStatus);
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
