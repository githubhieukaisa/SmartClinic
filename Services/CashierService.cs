using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
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

        // ─── Query ────────────────────────────────────────────────────────────────

        public async Task<List<PrescriptionQueueDto>> GetDispensedPrescriptionsAsync()
        {
            var tickets = await _context.QueueTickets
                .AsNoTracking()
                .Include(t => t.PatientUser)
                .Include(t => t.Doctor)
                .Include(t => t.Prescription)
                    .ThenInclude(p => p!.PrescriptionDetails)
                        .ThenInclude(d => d.Medicine)
                .Where(t => t.StatusEnum == TicketStatus.Completed)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();

            return tickets.Select(MapToDto).ToList();
        }

        // ─── Cash payment ─────────────────────────────────────────────────────────

        public async Task<(bool Success, string ErrorMessage)> ProcessPaymentAsync(int prescriptionId)
        {
            // prescriptionId here is actually the TicketId (mapped in DTO)
            return await FinalizePaymentAsync(prescriptionId, "Cash");
        }

        // ─── VNPay ────────────────────────────────────────────────────────────────

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

            // txnRef format: "{ticketId}_{timestamp}"
            var parts = txnRef.Split('_');
            if (!int.TryParse(parts[0], out var ticketId))
                return (false, "Invalid transaction reference.");

            return await FinalizePaymentAsync(ticketId, "VNPay");
        }

        // ─── Shared finalization ──────────────────────────────────────────────────

        private async Task<(bool Success, string ErrorMessage)> FinalizePaymentAsync(
            int ticketId, string paymentMethod)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CashierService] Finalizing payment ({paymentMethod}) for ticket #{ticketId}");
            try
            {
                var ticket = await _context.QueueTickets
                    .Include(t => t.PatientUser)
                    .Include(t => t.Prescription)
                    .FirstOrDefaultAsync(t => t.Id == ticketId);

                if (ticket == null)
                    return (false, "Queue ticket not found.");

                if (ticket.StatusEnum != TicketStatus.Completed)
                    return (false, $"Ticket is '{ticket.StatusEnum}', cannot process payment.");

                ticket.StatusEnum = TicketStatus.Done;

                if (ticket.Prescription != null)
                    ticket.Prescription.Status = PrescriptionStatus.Paid;

                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("PaymentCompleted", new
                {
                    prescriptionId = ticketId,
                    paymentMethod,
                    ticketNumber = ticket.TicketNumber,
                    patientName = ticket.PatientUser?.FullName
                });

                return (true, "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CashierService] ERROR: {ex.Message}");
                return (false, "System error during payment.");
            }
        }

        // ─── Helper ───────────────────────────────────────────────────────────────

        private static PrescriptionQueueDto MapToDto(QueueTicket t) => new()
        {
            // PrescriptionId is mapped to TicketId so the UI's payment callbacks work correctly
            PrescriptionId = t.Id,
            TicketId       = t.Id,
            TicketNumber   = t.TicketNumber,
            PatientName    = t.PatientUser?.FullName ?? "—",
            DoctorName     = t.Doctor?.FullName ?? "Not assigned",
            DoctorNote     = t.Prescription?.DoctorNote,
            Status         = t.StatusEnum.ToString(),
            // TotalAmount from QueueTicket (includes consultation + medicines + lab)
            TotalAmount    = t.TotalAmount ?? t.Prescription?.TotalAmount
                             ?? t.Prescription?.PrescriptionDetails
                                  .Sum(d => (d.UnitPrice > 0 ? d.UnitPrice : 0) * d.Quantity)
                             ?? 0,
            CreatedAt      = t.CreatedAt,
            Details        = t.Prescription?.PrescriptionDetails.Select(d => new PrescriptionDetailDto
            {
                DetailId         = d.Id,
                MedicineId       = d.MedicineId ?? 0,
                MedicineName     = d.Medicine?.Name ?? "—",
                Unit             = d.Medicine?.Unit,
                Quantity         = d.Quantity,
                UnitPrice        = d.UnitPrice > 0 ? d.UnitPrice : 0,
                UsageInstruction = d.UsageInstruction
            }).ToList() ?? new()
        };
    }
}