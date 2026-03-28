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

        public async Task<(bool Success, string ErrorMessage)> ProcessPaymentAsync(ProcessPaymentRequestDto request, int? cashierId = null)
        {
            return await FinalizePaymentAsync(request.TicketId, request.PaymentMethod, request.AmountReceived, cashierId);
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

            // For VNPay, amount received is practically exactly the total amount.
            // But we don't have it here directly, so we pass 0 and let Finalize override it or we can parse from vnp_Amount.
            decimal vnpAmount = 0;
            if (query.TryGetValue("vnp_Amount", out var amtStr) && decimal.TryParse(amtStr, out var amt))
            {
                vnpAmount = amt / 100m; // VNPay multiplies by 100
            }

            return await FinalizePaymentAsync(ticketId, "VNPay", vnpAmount, null);
        }

        // ─── Shared finalization ──────────────────────────────────────────────────

        private async Task<(bool Success, string ErrorMessage)> FinalizePaymentAsync(
            int ticketId, string paymentMethod, decimal amountReceived, int? cashierId)
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

                // Calculate the final exact amount required
                decimal exactTotal = ticket.Prescription?.TotalAmount 
                            ?? ticket.Prescription?.PrescriptionDetails.Sum(d => (d.UnitPrice > 0 ? d.UnitPrice : 0) * d.Quantity) 
                            ?? 0m;
                
                // For VNPay if amount is 0/missing, assume full payment.
                if (paymentMethod == "VNPay" && amountReceived == 0)
                    amountReceived = exactTotal;

                if (amountReceived < exactTotal && paymentMethod == "Cash")
                    return (false, $"Số tiền khách đưa ({amountReceived:N0}đ) không đủ để thanh toán ({exactTotal:N0}đ).");

                // ── Insert Payment Record ──
                var paymentRecord = new SmartClinic.Models.Entites.Payment
                {
                    TicketId = ticketId,
                    PaymentMethod = paymentMethod,
                    TotalAmount = exactTotal,
                    AmountReceived = amountReceived,
                    ChangeAmount = amountReceived - exactTotal,
                    CashierId = cashierId,
                    PaymentTime = DateTime.UtcNow,
                    Status = "Success"
                };
                
                _context.Payments.Add(paymentRecord);

                // ── Freeze TotalAmount to prevent future price changes & secure audit logs ──
                ticket.TotalAmount = exactTotal;
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

        // ─── Payment History ──────────────────────────────────────────────────────

        public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(DateTime date, int? cashierId = null)
        {
            // Do thời gian thanh toán lưu là UTC (DateTime.UtcNow), 
            // nên một giao dịch lúc 00:11 sáng nay (ICT) sẽ có giờ UTC là 17:11 ngày hôm qua.
            // Để chắc chắn lấy đủ, ta query một khoảng rộng hơn rồi lọc lại theo Giờ địa phương (ToLocalTime).
            var searchDate = date.Date;
            var minUtc = searchDate.AddDays(-1);
            var maxUtc = searchDate.AddDays(2);

            var query = _context.Payments
                .AsNoTracking()
                .Include(p => p.Ticket)
                    .ThenInclude(t => t!.PatientUser)
                .Include(p => p.Cashier)
                .Where(p => p.PaymentTime >= minUtc && p.PaymentTime < maxUtc);

            if (cashierId.HasValue)
            {
                query = query.Where(p => p.CashierId == cashierId.Value);
            }

            var results = await query
                .OrderByDescending(p => p.PaymentTime)
                .ToListAsync();

            // Lọc lại chính xác theo ngày địa phương của Server (vốn là múi giờ phòng khám)
            return results
                .Where(p => p.PaymentTime.ToLocalTime().Date == searchDate)
                .Select(p => new PaymentHistoryDto
                {
                    PaymentId = p.Id,
                    TicketId = p.TicketId,
                    PatientName = p.Ticket?.PatientUser?.FullName ?? "—",
                    PaymentMethod = p.PaymentMethod,
                    TotalAmount = p.TotalAmount,
                    AmountReceived = p.AmountReceived,
                    ChangeAmount = p.ChangeAmount,
                    Status = p.Status,
                    PaymentTime = p.PaymentTime,
                    CashierName = p.Cashier != null ? (p.Cashier.FullName ?? p.Cashier.Username) : "—"
                })
                .ToList();
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