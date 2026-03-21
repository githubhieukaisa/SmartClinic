using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.DTOs;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    /// <summary>
    /// Service xử lý nghiệp vụ Dược sĩ:
    /// - Lấy danh sách đơn thuốc chờ
    /// - Xuất thuốc (trừ tồn kho)
    /// - Gửi thông báo real-time cho Thu ngân
    ///
    /// Registered as Scoped (inject SmartClinicDbContext trực tiếp được)
    /// </summary>
    public class PharmacyService : IPharmacyService
    {
        private readonly SmartClinicDbContext _context;
        private readonly IHubContext<PrescriptionHub> _hubContext;

        public PharmacyService(SmartClinicDbContext context, IHubContext<PrescriptionHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // ─── QUERY ───────────────────────────────────────────────────────────────

        public async Task<List<PrescriptionQueueDto>> GetPendingPrescriptionsAsync()
        {
            var prescriptions = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.Doctor)   // Doctor qua QueueTicket.DoctorId
                .Include(p => p.PrescriptionDetails)
                    .ThenInclude(d => d.Medicine)
                .Where(p => p.Status == "Pending")
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            // Tính lại TotalAmount từ Medicine.Price × Quantity (fix UnitPrice = 0)
            foreach (var p in prescriptions)
            {
                if (p.TotalAmount == 0 && p.PrescriptionDetails.Any())
                {
                    p.TotalAmount = p.PrescriptionDetails
                        .Sum(d => d.Medicine != null ? d.Medicine.Price * d.Quantity : 0);
                }
            }

            return prescriptions.Select(MapToDto).ToList();
        }

        public async Task<PrescriptionQueueDto?> GetPrescriptionDetailAsync(int prescriptionId)
        {
            var prescription = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.Doctor)
                .Include(p => p.PrescriptionDetails)
                    .ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);

            return prescription == null ? null : MapToDto(prescription);
        }

        // ─── COMMAND ─────────────────────────────────────────────────────────────

        public async Task<(bool Success, string ErrorMessage)> DispenseMedicinesAsync(int prescriptionId)
        {
            System.Diagnostics.Debug.WriteLine($"[PharmacyService] Dispensing prescription {prescriptionId}");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var prescription = await _context.Prescriptions
                    .Include(p => p.Ticket)
                        .ThenInclude(t => t!.Patient)
                    .Include(p => p.Ticket)
                        .ThenInclude(t => t!.Doctor)
                    .Include(p => p.PrescriptionDetails)
                        .ThenInclude(d => d.Medicine)
                    .FirstOrDefaultAsync(p => p.Id == prescriptionId);

                if (prescription == null)
                    return (false, "Prescription not found.");

                if (prescription.Status != "Pending")
                    return (false, $"Prescription is '{prescription.Status}', cannot dispense.");

                decimal total = 0;

                // 1. Trừ tồn kho + gán UnitPrice từ Medicine.Price (fix UnitPrice = 0)
                foreach (var detail in prescription.PrescriptionDetails)
                {
                    if (detail.Medicine == null) continue;

                    if (detail.Medicine.StockQuantity < detail.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"'{detail.Medicine.Name}' insufficient stock (have {detail.Medicine.StockQuantity}, need {detail.Quantity}).");
                    }

                    // Gán đúng giá từ Medicine
                    detail.UnitPrice = detail.Medicine.Price;
                    detail.Medicine.StockQuantity -= detail.Quantity;
                    total += detail.Medicine.Price * detail.Quantity;
                }

                // 2. Cập nhật TotalAmount + Status
                prescription.TotalAmount = total;
                prescription.Status = "Dispensed";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 3. Thông báo real-time cho Thu ngân
                var notification = new PrescriptionDispensedNotificationDto
                {
                    PrescriptionId = prescription.Id,
                    TicketNumber = prescription.Ticket?.TicketNumber ?? 0,
                    PatientName = prescription.Ticket?.Patient?.FullName ?? "Unknown",
                    TotalAmount = total
                };

                await _hubContext.Clients.Group("Cashiers")
                    .SendAsync("PrescriptionDispensed", notification);

                System.Diagnostics.Debug.WriteLine($"[PharmacyService] Dispensed OK, total={total:N0}đ, notified Cashiers");
                return (true, "");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                System.Diagnostics.Debug.WriteLine($"[PharmacyService] ERROR: {ex.Message}");
                return (false, "System error during dispense.");
            }
        }

        /// <summary>
        /// Gọi sau khi bác sĩ lưu đơn thuốc thành công.
        /// Broadcast SignalR "NewPrescriptionReady" cho Dược sĩ.
        /// </summary>
        public async Task NotifyNewPrescriptionAsync(int prescriptionId)
        {
            var prescription = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.Doctor)
                .Include(p => p.PrescriptionDetails)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);

            if (prescription == null) return;

            var notification = new NewPrescriptionNotificationDto
            {
                PrescriptionId = prescription.Id,
                TicketNumber = prescription.Ticket?.TicketNumber ?? 0,
                PatientName = prescription.Ticket?.Patient?.FullName ?? "Unknown",
                DoctorName = prescription.Ticket?.Doctor?.FullName ?? "Unknown",
                MedicineCount = prescription.PrescriptionDetails.Count,
                TotalAmount = prescription.TotalAmount ?? 0
            };

            await _hubContext.Clients.Group("Pharmacists")
                .SendAsync("NewPrescriptionReady", notification);

            System.Diagnostics.Debug.WriteLine($"[PharmacyService] Notified Pharmacists: prescription {prescriptionId}");
        }

        // ─── HELPER ──────────────────────────────────────────────────────────────

        private static PrescriptionQueueDto MapToDto(Prescription p) => new()
        {
            PrescriptionId = p.Id,
            TicketId = p.TicketId ?? 0,
            TicketNumber = p.Ticket?.TicketNumber ?? 0,
            PatientName = p.Ticket?.Patient?.FullName ?? "—",
            // DoctorName lấy từ QueueTicket.Doctor (DoctorId assign khi bác sĩ gọi bệnh nhân)
            DoctorName = p.Ticket?.Doctor?.FullName ?? (p.Ticket?.Patient?.FullName != null ? "Not assigned" : "—"),
            DoctorNote = p.DoctorNote,
            Status = p.Status ?? "Pending",
            TotalAmount = p.TotalAmount ?? p.PrescriptionDetails.Sum(d => d.Medicine != null ? d.Medicine.Price * d.Quantity : d.UnitPrice * d.Quantity),
            CreatedAt = p.CreatedAt,
            Details = p.PrescriptionDetails.Select(d => new PrescriptionDetailDto
            {
                DetailId = d.Id,
                MedicineId = d.MedicineId ?? 0,
                MedicineName = d.Medicine?.Name ?? "—",
                Unit = d.Medicine?.Unit,
                Quantity = d.Quantity,
                // UnitPrice: dùng Medicine.Price nếu detail.UnitPrice chưa set (= 0)
                UnitPrice = d.UnitPrice > 0 ? d.UnitPrice : (d.Medicine?.Price ?? 0),
                UsageInstruction = d.UsageInstruction
            }).ToList()
        };
    }
}