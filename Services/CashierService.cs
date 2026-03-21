using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.DTOs;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public class CashierService : ICashierService
    {
        private readonly SmartClinicDbContext _context;
        private readonly IHubContext<PrescriptionHub> _hubContext;
        private readonly VNPayService _vnpay;

        public CashierService(SmartClinicDbContext context,
                               IHubContext<PrescriptionHub> hubContext,
                               VNPayService vnpay)
        {
            _context = context;
            _hubContext = hubContext;
            _vnpay = vnpay;
        }

        public async Task<List<PrescriptionQueueDto>> GetDispensedPrescriptionsAsync()
        {
            var prescriptions = await _context.Prescriptions
                .AsNoTracking()
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.Patient)
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.Doctor)
                .Include(p => p.PrescriptionDetails)
                    .ThenInclude(d => d.Medicine)
                .Where(p => p.Status == "Dispensed")
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return prescriptions.Select(MapToDto).ToList();
        }

        // ─── Cash payment ────────────────────────────────────────────────────────

        public async Task<(bool Success, string ErrorMessage)> ProcessPaymentAsync(int prescriptionId)
        {
            return await FinalizePaymentAsync(prescriptionId, "Cash");
        }

        // ─── VNPay ───────────────────────────────────────────────────────────────

        public string CreateVNPayUrl(int prescriptionId, decimal amount, string patientName)
        {
            return _vnpay.CreatePaymentUrl(prescriptionId, amount, patientName);
        }

        public async Task<(bool Success, string ErrorMessage)> HandleVNPayCallbackAsync(IQueryCollection query)
        {
            if (!_vnpay.ValidateSignature(query, out var txnRef, out var isSuccess))
                return (false, "Invalid VNPay signature.");

            if (!isSuccess)
                return (false, $"VNPay payment failed. Code: {query["vnp_ResponseCode"]}");

            // txnRef format: "{prescriptionId}_{timestamp}"
            var parts = txnRef.Split('_');
            if (!int.TryParse(parts[0], out var prescriptionId))
                return (false, "Invalid transaction reference.");

            return await FinalizePaymentAsync(prescriptionId, "VNPay");
        }

        // ─── Shared finalization ─────────────────────────────────────────────────

        private async Task<(bool Success, string ErrorMessage)> FinalizePaymentAsync(
            int prescriptionId, string paymentMethod)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CashierService] Finalizing payment ({paymentMethod}) for #{prescriptionId}");
            try
            {
                var prescription = await _context.Prescriptions
                    .Include(p => p.Ticket)
                        .ThenInclude(t => t!.Patient)
                    .FirstOrDefaultAsync(p => p.Id == prescriptionId);

                if (prescription == null)
                    return (false, "Prescription not found.");

                if (prescription.Status != "Dispensed")
                    return (false, $"Prescription is '{prescription.Status}', cannot process payment.");

                prescription.Status = "Paid";

                if (prescription.Ticket != null)
                    prescription.Ticket.Status = "Done";

                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("PaymentCompleted", new
                {
                    prescriptionId,
                    paymentMethod,
                    ticketNumber = prescription.Ticket?.TicketNumber,
                    patientName = prescription.Ticket?.Patient?.FullName
                });

                return (true, "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CashierService] ERROR: {ex.Message}");
                return (false, "System error during payment.");
            }
        }

        // ─── Helper ──────────────────────────────────────────────────────────────

        private static PrescriptionQueueDto MapToDto(Prescription p) => new()
        {
            PrescriptionId = p.Id,
            TicketId = p.TicketId ?? 0,
            TicketNumber = p.Ticket?.TicketNumber ?? 0,
            PatientName = p.Ticket?.Patient?.FullName ?? "—",
            DoctorName = p.Ticket?.Doctor?.FullName ?? "Not assigned",
            DoctorNote = p.DoctorNote,
            Status = p.Status ?? "Dispensed",
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