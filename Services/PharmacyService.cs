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
                    .ThenInclude(t => t!.Doctor)
                .Include(p => p.PrescriptionDetails)
                    .ThenInclude(d => d.Medicine)
                .Where(p => p.Status == "Pending")
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

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
                    return (false, "Không tìm thấy đơn thuốc.");

                if (prescription.Status != "Pending")
                    return (false, $"Đơn thuốc đang ở trạng thái '{prescription.Status}', không thể xuất.");

                // 1. Trừ tồn kho từng thuốc
                foreach (var detail in prescription.PrescriptionDetails)
                {
                    if (detail.Medicine == null) continue;

                    if (detail.Medicine.StockQuantity < detail.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Thuốc '{detail.Medicine.Name}' không đủ tồn kho (còn {detail.Medicine.StockQuantity}, cần {detail.Quantity}).");
                    }

                    detail.Medicine.StockQuantity -= detail.Quantity;
                }

                // 2. Cập nhật trạng thái đơn
                prescription.Status = "Dispensed";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 3. Thông báo real-time cho Thu ngân
                var notification = new PrescriptionDispensedNotificationDto
                {
                    PrescriptionId = prescription.Id,
                    TicketNumber = prescription.Ticket?.TicketNumber ?? 0,
                    PatientName = prescription.Ticket?.Patient?.FullName ?? "Unknown",
                    TotalAmount = prescription.TotalAmount ?? 0
                };

                await _hubContext.Clients.Group("Cashiers")
                    .SendAsync("PrescriptionDispensed", notification);

                System.Diagnostics.Debug.WriteLine($"[PharmacyService] Dispensed OK, notified Cashiers");
                return (true, "");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                System.Diagnostics.Debug.WriteLine($"[PharmacyService] ERROR: {ex.Message}");
                return (false, "Lỗi hệ thống khi xuất thuốc.");
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
            DoctorName = p.Ticket?.Doctor?.FullName ?? "—",
            DoctorNote = p.DoctorNote,
            Status = p.Status ?? "Pending",
            TotalAmount = p.TotalAmount ?? 0,
            CreatedAt = p.CreatedAt,
            Details = p.PrescriptionDetails.Select(d => new PrescriptionDetailDto
            {
                DetailId = d.Id,
                MedicineId = d.MedicineId ?? 0,
                MedicineName = d.Medicine?.Name ?? "—",
                Unit = d.Medicine?.Unit,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                UsageInstruction = d.UsageInstruction
            }).ToList()
        };
    }
}