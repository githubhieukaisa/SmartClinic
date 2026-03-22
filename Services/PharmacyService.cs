using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.DTOs;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class PharmacyService : IPharmacyService
    {
        private readonly SmartClinicDbContext _context;
        private readonly IHubContext<PrescriptionHub> _hubContext;

        public PharmacyService(SmartClinicDbContext context, IHubContext<PrescriptionHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // ─── QUERY ──────────────────────────────────────────────────────────────

        public async Task<List<PrescriptionQueueDto>> GetPendingPrescriptionsAsync()
        {
            var prescriptions = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket).ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket).ThenInclude(t => t!.Doctor)
                .Include(p => p.PrescriptionDetails).ThenInclude(d => d.Medicine)
                .Where(p => p.Status == "Pending")
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return prescriptions.Select(MapToDto).ToList();
        }

        public async Task<PrescriptionQueueDto?> GetPrescriptionDetailAsync(int prescriptionId)
        {
            var p = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket).ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket).ThenInclude(t => t!.Doctor)
                .Include(p => p.PrescriptionDetails).ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);
            return p == null ? null : MapToDto(p);
        }

        // ─── DISPENSE ───────────────────────────────────────────────────────────

        public async Task<(bool Success, string ErrorMessage)> DispenseMedicinesAsync(int prescriptionId)
        {
            System.Diagnostics.Debug.WriteLine($"[PharmacyService] Dispensing #{prescriptionId}");
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Load WITH tracking (không AsNoTracking) để EF detect thay đổi
                var prescription = await _context.Prescriptions
                    .Include(p => p.Ticket).ThenInclude(t => t!.Patient)
                    .Include(p => p.Ticket).ThenInclude(t => t!.Doctor)
                    .Include(p => p.PrescriptionDetails).ThenInclude(d => d.Medicine)
                    .FirstOrDefaultAsync(p => p.Id == prescriptionId);

                if (prescription == null)
                    return (false, "Prescription not found.");
                if (prescription.Status != "Pending")
                    return (false, $"Prescription is '{prescription.Status}', cannot dispense.");

                decimal total = 0;

                foreach (var detail in prescription.PrescriptionDetails)
                {
                    if (detail.Medicine == null) continue;

                    if (detail.Medicine.StockQuantity < detail.Quantity)
                    {
                        await tx.RollbackAsync();
                        return (false, $"'{detail.Medicine.Name}' insufficient stock " +
                                       $"(have {detail.Medicine.StockQuantity}, need {detail.Quantity}).");
                    }

                    // Gán giá + trừ kho
                    detail.UnitPrice = detail.Medicine.Price;
                    detail.Medicine.StockQuantity -= detail.Quantity;
                    total += detail.Medicine.Price * detail.Quantity;
                }

                prescription.TotalAmount = total;
                prescription.Status = "Dispensed";

                // SaveChanges sẽ persist tất cả: UnitPrice, StockQuantity, Status, TotalAmount
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                await _hubContext.Clients.Group("Cashiers").SendAsync("PrescriptionDispensed",
                    new PrescriptionDispensedNotificationDto
                    {
                        PrescriptionId = prescription.Id,
                        TicketNumber = prescription.Ticket?.TicketNumber ?? 0,
                        PatientName = prescription.Ticket?.Patient?.FullName ?? "Unknown",
                        TotalAmount = total
                    });

                System.Diagnostics.Debug.WriteLine($"[PharmacyService] OK total={total:N0}đ");
                return (true, "");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                System.Diagnostics.Debug.WriteLine($"[PharmacyService] ERROR: {ex.Message}");
                return (false, $"System error: {ex.Message}");
            }
        }

        // ─── MEDICINE CRUD ───────────────────────────────────────────────────────

        public async Task<List<Medicine>> GetAllMedicinesAsync()
            => await _context.Medicines.OrderBy(m => m.Name).ToListAsync();

        public async Task<Medicine?> GetMedicineByIdAsync(int id)
            => await _context.Medicines.FindAsync(id);

        public async Task<Medicine> CreateMedicineAsync(Medicine medicine)
        {
            _context.Medicines.Add(medicine);
            await _context.SaveChangesAsync();
            return medicine;
        }

        public async Task<(bool Success, string Error)> UpdateMedicineAsync(Medicine medicine)
        {
            var existing = await _context.Medicines.FindAsync(medicine.Id);
            if (existing == null) return (false, "Medicine not found.");
            existing.Name = medicine.Name;
            existing.Unit = medicine.Unit;
            existing.Price = medicine.Price;
            existing.StockQuantity = medicine.StockQuantity;
            await _context.SaveChangesAsync();
            return (true, "");
        }

        public async Task<(bool Success, string Error)> DeleteMedicineAsync(int id)
        {
            var medicine = await _context.Medicines.FindAsync(id);
            if (medicine == null) return (false, "Medicine not found.");
            _context.Medicines.Remove(medicine);
            await _context.SaveChangesAsync();
            return (true, "");
        }

        // ─── NOTIFY ─────────────────────────────────────────────────────────────

        public async Task NotifyNewPrescriptionAsync(int prescriptionId)
        {
            var p = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket).ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket).ThenInclude(t => t!.Doctor)
                .Include(p => p.PrescriptionDetails).ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);
            if (p == null) return;

            await _hubContext.Clients.Group("Pharmacists").SendAsync("NewPrescriptionReady",
                new NewPrescriptionNotificationDto
                {
                    PrescriptionId = p.Id,
                    TicketNumber = p.Ticket?.TicketNumber ?? 0,
                    PatientName = p.Ticket?.Patient?.FullName ?? "Unknown",
                    DoctorName = p.Ticket?.Doctor?.FullName ?? "Unknown",
                    MedicineCount = p.PrescriptionDetails.Count,
                    TotalAmount = p.TotalAmount ?? 0
                });
        }

        public async Task NotifyPrescriptionDeletedAsync(int prescriptionId)
        {
            await _hubContext.Clients.Group("Pharmacists").SendAsync("PrescriptionDeleted", prescriptionId);
        }

        // ─── HELPER ─────────────────────────────────────────────────────────────

        private static PrescriptionQueueDto MapToDto(Prescription p) => new()
        {
            PrescriptionId = p.Id,
            TicketId = p.TicketId ?? 0,
            TicketNumber = p.Ticket?.TicketNumber ?? 0,
            PatientName = p.Ticket?.Patient?.FullName ?? "—",
            DoctorName = p.Ticket?.Doctor?.FullName ?? "Not assigned",
            DoctorNote = p.DoctorNote,
            Status = p.Status ?? "Pending",
            TotalAmount = p.TotalAmount ?? p.PrescriptionDetails
                                .Sum(d => (d.UnitPrice > 0 ? d.UnitPrice : d.Medicine?.Price ?? 0) * d.Quantity),
            CreatedAt = p.CreatedAt,
            Details = p.PrescriptionDetails.Select(d => new PrescriptionDetailDto
            {
                DetailId = d.Id,
                MedicineId = d.MedicineId ?? 0,
                MedicineName = d.Medicine?.Name ?? "—",
                Unit = d.Medicine?.Unit,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice > 0 ? d.UnitPrice : (d.Medicine?.Price ?? 0),
                UsageInstruction = d.UsageInstruction
            }).ToList()
        };
    }
}