using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
using SmartClinic.Hubs;
using SmartClinic.Models;
using SmartClinic.DTOs;

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
        /// Get doctor's current active shift with strict time and status checking
        /// </summary>
        public async Task<DoctorShift?> GetActiveDoctorShiftAsync(int doctorId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var now = DateTime.Now;

            return await context.DoctorShifts
                .AsNoTracking()
                .Include(s => s.Room)
                .ThenInclude(r => r.Department)
                .Where(s => s.DoctorId == doctorId
                         && s.StatusEnum == DoctorShiftStatus.Active
                         && s.StartTime <= now
                         && (s.EndTime == null || s.EndTime >= now))
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync();
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
                // STEP 1: Get doctor's current active shift using centralized logic
                var doctorShift = await GetActiveDoctorShiftAsync(doctorId);
                var roomId = doctorShift?.RoomId;

                // STEP 2: Get all relevant queue tickets
                // Unified logic: Room common queue (Today only) OR Doctor's private active patients (No date filter)
                var today = DateTime.Today;
                var tickets = await context.QueueTickets
                    .AsNoTracking()
                    .Include(t => t.Patient)
                    .Where(t =>
                        // TH1: Hàng chờ chung của phòng (Chờ khám, Đang gọi) - Chỉ trong ngày + Phải có Shift
                        (roomId != null && t.RoomId == roomId && (t.StatusEnum == TicketStatus.Waiting || t.StatusEnum == TicketStatus.Calling) && t.CreatedAt >= today)
                        ||
                        // TH2: Bệnh nhân riêng của bác sĩ (Đang khám, Đang xét nghiệm) - KHÔNG LỌC NGÀY
                        (t.DoctorId == doctorId && (t.StatusEnum == TicketStatus.Examinating || t.StatusEnum == TicketStatus.Testing))
                    )
                    .ToListAsync();

                // Sort by priority: Examining → Testing → Calling → Waiting
                var sortedTickets = tickets
                    .OrderBy(t => 
                        t.StatusEnum == TicketStatus.Examinating ? 0 : 
                        t.StatusEnum == TicketStatus.Testing ? 1 : 
                        t.StatusEnum == TicketStatus.Calling ? 2 : 3)
                    .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                    .ToList();
 
                 System.Diagnostics.Debug.WriteLine($"✅ [PatientService] Found {sortedTickets.Count} queue tickets");
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
                    Diagnosis = diagnosis,
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

        /// <summary>
        /// Get summary data for the doctor dashboard
        /// </summary>
        public async Task<DoctorDashboardDto> GetDoctorDashboardSummaryAsync(int doctorId, string period = "Day")
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var today = DateTime.Today;
            var now = DateTime.Now;

            // Xác định mốc thời gian dựa theo Filter
            DateTime filterStartDate = today;
            if (period == "Week")
            {
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                filterStartDate = today.AddDays(-1 * diff).Date;
            }
            else if (period == "Month")
            {
                filterStartDate = new DateTime(today.Year, today.Month, 1);
            }

            var shift = await GetActiveDoctorShiftAsync(doctorId);
            var roomId = shift?.RoomId;

            // Query data - Unified logic
            var tickets = await context.QueueTickets
                .AsNoTracking()
                .Include(t => t.Patient)
                .Where(t =>
                    // TH1: Hàng chờ chung của phòng (Waiting/Calling) - Chỉ trong ngày
                    (roomId != null && t.RoomId == roomId && (t.StatusEnum == TicketStatus.Waiting || t.StatusEnum == TicketStatus.Calling) && t.CreatedAt >= today)
                    ||
                    // TH2: Bệnh nhân đang khám/xét nghiệm của BS - KHÔNG LỌC NGÀY
                    (t.DoctorId == doctorId && (t.StatusEnum == TicketStatus.Examinating || t.StatusEnum == TicketStatus.Testing))
                    ||
                    // TH3: Bệnh nhân đã xong (Completed/Done) - Lọc theo Period (Day/Week/Month)
                    (t.DoctorId == doctorId && (t.StatusEnum == TicketStatus.Completed || t.StatusEnum == TicketStatus.Done) && t.CreatedAt >= filterStartDate)
                )
                .Select(t => new
                {
                    t.Id,
                    t.TicketNumber,
                    t.PatientId,
                    t.DoctorId,
                    t.StatusEnum,
                    t.CreatedAt,
                    t.UpdatedAt,
                    PatientName = t.Patient != null ? t.Patient.FullName : "N/A",
                    PatientPhone = t.Patient != null ? t.Patient.Phone : ""
                })
                .ToListAsync();

            var result = new DoctorDashboardDto();

            // Only count today's tickets for summary unless period is different
            var dashboardTickets = tickets.Where(t => t.CreatedAt >= filterStartDate).ToList();

            result.TotalCount = dashboardTickets.Count;
            result.WaitingCount = dashboardTickets.Count(t => t.StatusEnum == TicketStatus.Waiting || t.StatusEnum == TicketStatus.Calling);
            result.InProgressCount = dashboardTickets.Count(t => t.StatusEnum == TicketStatus.Examinating || t.StatusEnum == TicketStatus.Testing);

            result.TestingTickets = tickets
                .Where(t => t.StatusEnum == TicketStatus.Testing && t.DoctorId == doctorId)
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .Take(5)
                .Select(t => new QueueTicket
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber,
                    PatientId = t.PatientId,
                    StatusEnum = t.StatusEnum,
                    Patient = new Patient { Id = t.PatientId ?? 0, FullName = t.PatientName, Phone = t.PatientPhone }
                })
                .ToList();

            result.RecentCompletedTickets = dashboardTickets
                .Where(t => t.StatusEnum == TicketStatus.Completed || t.StatusEnum == TicketStatus.Done)
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .Take(5)
                .Select(t => new QueueTicket
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber,
                    PatientId = t.PatientId,
                    StatusEnum = t.StatusEnum,
                    UpdatedAt = t.UpdatedAt,
                    CreatedAt = t.CreatedAt,
                    Patient = new Patient { Id = t.PatientId ?? 0, FullName = t.PatientName }
                })
                .ToList();

            return result;
        }
    }
}
