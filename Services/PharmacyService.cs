using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.DTOs;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    /// <summary>
    /// Handles the full pharmacy workflow:
    ///   Doctor saves prescription  →  pharmacist dispenses  →  cashier collects payment.
    ///
    /// Uses IDbContextFactory so every method gets its own fresh DbContext —
    /// safe to call from SignalR callbacks and background services.
    /// </summary>
    public class PharmacyService : IPharmacyService
    {
        private readonly IDbContextFactory<SmartClinicDbContext> _contextFactory;
        private readonly IHubContext<PrescriptionHub> _prescriptionHub;

        public PharmacyService(
            IDbContextFactory<SmartClinicDbContext> contextFactory,
            IHubContext<PrescriptionHub> prescriptionHub)
        {
            _contextFactory = contextFactory;
            _prescriptionHub = prescriptionHub;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  DOCTOR: Save Prescription
        // ──────────────────────────────────────────────────────────────────────

        public async Task<Prescription> SavePrescriptionAsync(
            int ticketId,
            int doctorId,
            string? doctorNote,
            List<PrescriptionItemRequest> items)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();

            // Upsert: one prescription per ticket (unique index in DB)
            var prescription = await ctx.Prescriptions
                .Include(p => p.PrescriptionDetails)
                .FirstOrDefaultAsync(p => p.TicketId == ticketId);

            if (prescription == null)
            {
                prescription = new Prescription { TicketId = ticketId };
                ctx.Prescriptions.Add(prescription);
            }
            else
            {
                // Remove existing details to replace them
                ctx.PrescriptionDetails.RemoveRange(prescription.PrescriptionDetails);
            }

            prescription.DoctorNote = doctorNote;
            prescription.Status = "Pending"; // awaiting pharmacist

            // Re-add details and calculate total
            decimal total = 0;
            var newDetails = new List<PrescriptionDetail>();
            foreach (var item in items)
            {
                var detail = new PrescriptionDetail
                {
                    MedicineId = item.MedicineId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    UsageInstruction = item.UsageInstruction
                };
                total += item.Quantity * item.UnitPrice;
                newDetails.Add(detail);
            }
            prescription.TotalAmount = total;
            prescription.PrescriptionDetails = newDetails;

            await ctx.SaveChangesAsync();

            // Broadcast to pharmacists
            var ticket = await ctx.QueueTickets
                .Include(t => t.Patient)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            var doctor = await ctx.Users.FindAsync(doctorId);

            var notification = new NewPrescriptionNotificationDto
            {
                PrescriptionId = prescription.Id,
                TicketId = ticketId,
                PatientName = ticket?.Patient?.FullName ?? "Unknown",
                DoctorName = doctor?.FullName ?? "Unknown Doctor",
                MedicineCount = items.Count,
                TotalAmount = total,
                CreatedAt = DateTime.Now
            };

            await _prescriptionHub.Clients.Group("Pharmacists")
                .SendAsync("NewPrescriptionReady", notification);

            return prescription;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  PHARMACIST: Dispense Prescription
        // ──────────────────────────────────────────────────────────────────────

        public async Task DispensePrescriptionAsync(int prescriptionId, int pharmacistId)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();

            var prescription = await ctx.Prescriptions
                .Include(p => p.PrescriptionDetails)
                    .ThenInclude(d => d.Medicine)
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.Patient)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId)
                ?? throw new InvalidOperationException($"Prescription {prescriptionId} not found.");

            if (prescription.Status != "Pending")
                throw new InvalidOperationException("Prescription is not in Pending state.");

            // Decrement stock for each medicine
            foreach (var detail in prescription.PrescriptionDetails)
            {
                if (detail.Medicine == null) continue;

                if (detail.Medicine.StockQuantity < detail.Quantity)
                    throw new InvalidOperationException(
                        $"Insufficient stock for {detail.Medicine.Name}. " +
                        $"Available: {detail.Medicine.StockQuantity}, Required: {detail.Quantity}.");

                detail.Medicine.StockQuantity -= detail.Quantity;
            }

            prescription.Status = "Dispensed";
            await ctx.SaveChangesAsync();

            // Notify cashiers
            var notification = new PrescriptionDispensedNotificationDto
            {
                PrescriptionId = prescriptionId,
                TicketId = prescription.TicketId ?? 0,
                PatientName = prescription.Ticket?.Patient?.FullName ?? "Unknown",
                TotalAmount = prescription.TotalAmount ?? 0
            };

            await _prescriptionHub.Clients.Group("Cashiers")
                .SendAsync("PrescriptionDispensed", notification);
        }

        // ──────────────────────────────────────────────────────────────────────
        //  CASHIER: Pay Prescription
        // ──────────────────────────────────────────────────────────────────────

        public async Task PayPrescriptionAsync(int prescriptionId, int cashierId)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();

            var prescription = await ctx.Prescriptions
                .Include(p => p.Ticket)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId)
                ?? throw new InvalidOperationException($"Prescription {prescriptionId} not found.");

            if (prescription.Status != "Dispensed")
                throw new InvalidOperationException("Prescription must be Dispensed before payment.");

            prescription.Status = "Paid";

            // Mark QueueTicket as Done
            if (prescription.Ticket != null)
                prescription.Ticket.Status = "Done";

            await ctx.SaveChangesAsync();
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Queries
        // ──────────────────────────────────────────────────────────────────────

        public async Task<List<PrescriptionSummaryDto>> GetPendingPrescriptionsAsync()
            => await QueryPrescriptionsAsync("Pending");

        public async Task<List<PrescriptionSummaryDto>> GetDispensedPrescriptionsAsync()
            => await QueryPrescriptionsAsync("Dispensed");

        public async Task<PrescriptionSummaryDto?> GetPrescriptionDetailAsync(int prescriptionId)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();

            var p = await ctx.Prescriptions
                .AsNoTracking()
                .Include(x => x.Ticket).ThenInclude(t => t!.Patient)
                .Include(x => x.Ticket).ThenInclude(t => t!.Doctor)
                .Include(x => x.PrescriptionDetails).ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(x => x.Id == prescriptionId);

            return p == null ? null : MapToSummary(p);
        }

        public async Task<List<PaymentSummaryDto>> GetPrescriptionsForCashierAsync()
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();

            return await ctx.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket).ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket).ThenInclude(t => t!.Doctor)
                .Where(p => p.Status == "Dispensed" || p.Status == "Paid")
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PaymentSummaryDto
                {
                    PrescriptionId = p.Id,
                    TicketId = p.TicketId ?? 0,
                    PatientName = p.Ticket != null && p.Ticket.Patient != null
                        ? p.Ticket.Patient.FullName : "Unknown",
                    DoctorName = p.Ticket != null && p.Ticket.Doctor != null
                        ? p.Ticket.Doctor.FullName ?? "Unknown" : "Unknown",
                    TotalAmount = p.TotalAmount ?? 0,
                    Status = p.Status ?? "Unknown",
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────────────────────────────

        private async Task<List<PrescriptionSummaryDto>> QueryPrescriptionsAsync(string status)
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();

            var list = await ctx.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket).ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket).ThenInclude(t => t!.Doctor)
                .Include(p => p.PrescriptionDetails).ThenInclude(d => d.Medicine)
                .Where(p => p.Status == status)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return list.Select(MapToSummary).ToList();
        }

        private static PrescriptionSummaryDto MapToSummary(Prescription p) => new()
        {
            PrescriptionId = p.Id,
            TicketId = p.TicketId ?? 0,
            PatientName = p.Ticket?.Patient?.FullName ?? "Unknown",
            DoctorName = p.Ticket?.Doctor?.FullName ?? "Unknown",
            Status = p.Status ?? "",
            TotalAmount = p.TotalAmount ?? 0,
            CreatedAt = p.CreatedAt,
            Details = p.PrescriptionDetails.Select(d => new PrescriptionDetailDto
            {
                MedicineId = d.MedicineId ?? 0,
                MedicineName = d.Medicine?.Name ?? "Unknown",
                Unit = d.Medicine?.Unit,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                UsageInstruction = d.UsageInstruction
            }).ToList()
        };
    }
}