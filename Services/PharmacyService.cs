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
                .Include(p => p.PrescriptionDetails).ThenInclude(d => d.Medicine).ThenInclude(m => m!.MedicinePrices)
                .Where(p => p.Status == PrescriptionStatus.Pending)
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
                .Include(p => p.PrescriptionDetails).ThenInclude(d => d.Medicine).ThenInclude(m => m!.MedicinePrices)
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
                    .Include(p => p.PrescriptionDetails)
                        .ThenInclude(d => d.Medicine)
                            .ThenInclude(m => m!.MedicinePrices)
                    .FirstOrDefaultAsync(p => p.Id == prescriptionId);

                if (prescription == null)
                    return (false, "Prescription not found.");
                if (prescription.Status != PrescriptionStatus.Pending)
                    return (false, $"Prescription is '{prescription.Status}', cannot dispense.");

                decimal total = 0;

                foreach (var detail in prescription.PrescriptionDetails)
                {
                    if (detail.Medicine == null) continue;

                    // Kiểm tra thuốc đang bán và đủ số lượng
                    if (!detail.Medicine.IsForSale)
                    {
                        await tx.RollbackAsync();
                        return (false, $"'{detail.Medicine.Name}' is currently not for sale.");
                    }

                    if (detail.Medicine.PhysicalStock < detail.Quantity)
                    {
                        await tx.RollbackAsync();
                        return (false, $"'{detail.Medicine.Name}' insufficient stock " +
                                       $"(have {detail.Medicine.PhysicalStock}, need {detail.Quantity}).");
                    }

                    // Lấy giá mới nhất từ MedicinePrice
                    var currentPrice = detail.Medicine.MedicinePrices
                        .OrderByDescending(p => p.EffectiveFrom)
                        .FirstOrDefault()?.Price ?? 0m;

                    // Gán giá snapshot + trừ kho
                    detail.UnitPrice = currentPrice;
                    detail.Medicine.StockQuantity -= detail.Quantity; // giảm signed int
                    total += currentPrice * detail.Quantity;
                }

                prescription.TotalAmount = total;
                prescription.Status = PrescriptionStatus.Dispensed;

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
            => await _context.Medicines
                    .Include(m => m.MedicinePrices)
                    .OrderBy(m => m.Name)
                    .ToListAsync();

        public async Task<Medicine?> GetMedicineByIdAsync(int id)
            => await _context.Medicines.FindAsync(id);

        public async Task<Medicine> CreateMedicineWithPriceAsync(Medicine medicine, decimal initialPrice)
        {
            medicine.MedicinePrices = new List<MedicinePrice>
            {
                new MedicinePrice { Price = initialPrice, EffectiveFrom = DateTime.UtcNow }
            };
            _context.Medicines.Add(medicine);
            await _context.SaveChangesAsync();
            return medicine;
        }

        public async Task<(bool Success, string Error)> UpdateMedicineWithPriceAsync(Medicine medicine, decimal newPrice)
        {
            var existing = await _context.Medicines
                .Include(m => m.MedicinePrices)
                .FirstOrDefaultAsync(m => m.Id == medicine.Id);
                
            if (existing == null) return (false, "Medicine not found.");
            
            existing.Name = medicine.Name;
            existing.Unit = medicine.Unit;
            existing.StockQuantity = medicine.StockQuantity;

            // Check if price changed
            var currentPrice = existing.MedicinePrices.OrderByDescending(p => p.EffectiveFrom).FirstOrDefault()?.Price ?? 0m;
            if (currentPrice != newPrice)
            {
                existing.MedicinePrices.Add(new MedicinePrice { Price = newPrice, EffectiveFrom = DateTime.UtcNow });
            }

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

        public async Task<(bool Success, string Error)> ToggleMedicineForSaleAsync(int id)
        {
            var medicine = await _context.Medicines.FindAsync(id);
            if (medicine == null) return (false, "Medicine not found.");
            // Đảo bit: dương ↔ âm, giữ nguyên số lượng vật lý
            medicine.StockQuantity = ~medicine.StockQuantity;
            await _context.SaveChangesAsync();
            return (true, "");
        }

        public async Task<List<MedicineInsightDto>> GetMedicineInsightsAsync(DateTime fromDate, DateTime toDate)
        {
            var start = fromDate.Date;
            var end = toDate.Date.AddDays(1).AddTicks(-1);

            // Fetch the quantities sold in the last X days for prescriptions that are Dispensed (or Paid)
            var salesData = await _context.PrescriptionDetails
                .Where(d => d.Prescription.CreatedAt >= start && d.Prescription.CreatedAt <= end &&
                            d.Prescription.Status != PrescriptionStatus.Pending) // Only count dispensed/paid
                .GroupBy(d => d.MedicineId)
                .Select(g => new 
                {
                    MedicineId = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .ToListAsync();

            var medicines = await _context.Medicines.ToListAsync();

            var insights = medicines.Select(m => {
                var sale = salesData.FirstOrDefault(s => s.MedicineId == m.Id);
                return new MedicineInsightDto
                {
                    MedicineId = m.Id,
                    MedicineName = m.Name,
                    Unit = m.Unit ?? "—",
                    CurrentStock = m.PhysicalStock,
                    IsForSale = m.IsForSale,
                    QuantitySold = sale?.TotalQuantity ?? 0,
                    Revenue = sale?.TotalRevenue ?? 0m
                };
            }).ToList();

            return insights;
        }

        // ─── NOTIFY ─────────────────────────────────────────────────────────────

        public async Task NotifyNewPrescriptionAsync(int prescriptionId)
        {
            var p = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket).ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket).ThenInclude(t => t!.Doctor)
                .Include(p => p.PrescriptionDetails).ThenInclude(d => d.Medicine).ThenInclude(m => m!.MedicinePrices)
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
                    TotalAmount = p.TotalAmount ?? p.PrescriptionDetails.Sum(d => (d.UnitPrice > 0 ? d.UnitPrice : (d.Medicine?.MedicinePrices.OrderByDescending(x => x.EffectiveFrom).FirstOrDefault()?.Price ?? 0m)) * d.Quantity)
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
            Status = p.Status.ToString(),
            TotalAmount = p.TotalAmount ?? p.PrescriptionDetails
                                .Sum(d => (d.UnitPrice > 0 ? d.UnitPrice : (d.Medicine?.MedicinePrices.OrderByDescending(x => x.EffectiveFrom).FirstOrDefault()?.Price ?? 0m)) * d.Quantity),
            CreatedAt = p.CreatedAt,
            Details = p.PrescriptionDetails.Select(d => new PrescriptionDetailDto
            {
                DetailId = d.Id,
                MedicineId = d.MedicineId ?? 0,
                MedicineName = d.Medicine?.Name ?? "—",
                Unit = d.Medicine?.Unit,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice > 0 ? d.UnitPrice : (d.Medicine?.MedicinePrices.OrderByDescending(x => x.EffectiveFrom).FirstOrDefault()?.Price ?? 0m),
                UsageInstruction = d.UsageInstruction
            }).ToList()
        };
    }
}