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
        private readonly IHubContext<PatientHub> _patientHubContext;
        private readonly VNPayService _vnpay;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CashierService(SmartClinicDbContext context,
                               IHubContext<PrescriptionHub> hubContext,
                               IHubContext<PatientHub> patientHubContext,
                               VNPayService vnpay,
                               IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _hubContext = hubContext;
            _patientHubContext = patientHubContext;
            _vnpay = vnpay;
            _httpContextAccessor = httpContextAccessor;
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
                .Include(t => t.LabOrders)
                    .ThenInclude(lo => lo.LabOrderDetails)
                .Where(t => t.StatusEnum == TicketStatus.Completed)
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
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

            // Capture VNPay transaction number for audit trail
            _vnpTxnNo = query.TryGetValue("vnp_TransactionNo", out var txnNo) ? txnNo.ToString() : null;

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
                        .ThenInclude(p => p!.PrescriptionDetails)
                    .Include(t => t.LabOrders)
                        .ThenInclude(lo => lo.LabOrderDetails)
                    .FirstOrDefaultAsync(t => t.Id == ticketId);

                if (ticket == null)
                    return (false, "Queue ticket not found.");

                if (ticket.StatusEnum != TicketStatus.Completed)
                    return (false, $"Ticket is '{ticket.StatusEnum}', cannot process payment.");

                // ── Anti-collusion: Block self-payment ──
                if (cashierId.HasValue && ticket.PatientId.HasValue && cashierId.Value == ticket.PatientId.Value)
                    return (false, "Thu ngân không được thanh toán cho chính mình.");

                // ─── SOURCE OF TRUTH: Read total amount from QueueTicket ───
                // Using the amount calculated and saved by the Doctor/System.
                decimal exactTotal = ticket.TotalAmount ?? 0m;
                
                // Fallback only if for some reason Ticket.TotalAmount is missing
                if (exactTotal <= 0)
                {
                    decimal medicineAmount = ticket.Prescription?.PrescriptionDetails.Sum(d => (d.UnitPrice > 0 ? d.UnitPrice : 0) * d.Quantity) ?? 0m;
                    decimal labTestAmount = ticket.LabOrders.SelectMany(lo => lo.LabOrderDetails).Sum(lod => lod.UnitPrice);
                    decimal consultationFee = 300000m; // Standard fallback
                    exactTotal = consultationFee + medicineAmount + labTestAmount;
                }
                
                // For VNPay if amount is 0/missing, assume full payment.
                if (paymentMethod == "VNPay" && amountReceived == 0)
                    amountReceived = exactTotal;

                if (amountReceived < exactTotal && paymentMethod == "Cash")
                    return (false, $"Số tiền khách đưa ({amountReceived:N0}đ) không đủ để thanh toán ({exactTotal:N0}đ).");

                // ── Capture audit trail ──
                var clientIp = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

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
                    Status = "Success",
                    VnpTransactionNo = _vnpTxnNo,   // captured from HandleVNPayCallbackAsync
                    IpAddress = clientIp
                };
                
                _context.Payments.Add(paymentRecord);

                // ── Update Ticket Status ──
                ticket.StatusEnum = TicketStatus.Done;
                // Note: ticket.TotalAmount is preserved as the source of truth.
                // If it was null, we update it with our fallback calculation.
                if (!ticket.TotalAmount.HasValue || ticket.TotalAmount <= 0)
                {
                    ticket.TotalAmount = exactTotal;
                }

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

                // ── SignalR: Notify Patient ──
                if (ticket.PatientId.HasValue)
                {
                    await _patientHubContext.Clients.All.SendAsync("QueueStatusUpdated", new { patientId = ticket.PatientId.Value, status = "Paid" });
                }

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

        // ─── Manager Reconciliation ──────────────────────────────────────────────

        public async Task<List<CashierDailySummaryDto>> GetDailySummariesAsync(DateTime date)
        {
            var searchDate = date.Date;
            var minUtc = searchDate.AddDays(-1);
            var maxUtc = searchDate.AddDays(2);

            var payments = await _context.Payments
                .AsNoTracking()
                .Include(p => p.Ticket).ThenInclude(t => t!.PatientUser)
                .Include(p => p.Cashier)
                .Where(p => p.PaymentTime >= minUtc && p.PaymentTime < maxUtc)
                .OrderByDescending(p => p.PaymentTime)
                .ToListAsync();

            var filtered = payments.Where(p => p.PaymentTime.ToLocalTime().Date == searchDate).ToList();

            var reconciliations = await _context.CashierReconciliations
                .AsNoTracking()
                .Include(r => r.ConfirmedByUser)
                .Where(r => r.ReportDate == searchDate)
                .ToListAsync();

            var grouped = filtered
                .GroupBy(p => p.CashierId ?? 0)
                .Select(g =>
                {
                    var cashierName = g.First().Cashier?.FullName ?? g.First().Cashier?.Username ?? "VNPay (tự động)";
                    var recon = reconciliations.FirstOrDefault(r => r.CashierId == g.Key);

                    return new CashierDailySummaryDto
                    {
                        CashierId = g.Key,
                        CashierName = cashierName,
                        ReportDate = searchDate,
                        TransactionCount = g.Count(),
                        CashTotal = g.Where(p => p.PaymentMethod == "Cash").Sum(p => p.TotalAmount),
                        VNPayTotal = g.Where(p => p.PaymentMethod == "VNPay").Sum(p => p.TotalAmount),
                        IsConfirmed = recon?.IsConfirmed ?? false,
                        ConfirmedByName = recon?.ConfirmedByUser?.FullName,
                        ConfirmedAt = recon?.ConfirmedAt,
                        Note = recon?.Note,
                        Details = g.Select(p => new PaymentHistoryDto
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
                            CashierName = cashierName
                        }).ToList()
                    };
                })
                .OrderBy(s => s.CashierName)
                .ToList();

            return grouped;
        }

        public async Task<(bool Success, string Error)> ConfirmReconciliationAsync(
            int cashierId, DateTime date, int confirmedByUserId, string? note)
        {
            var reportDate = date.Date;

            var existing = await _context.CashierReconciliations
                .FirstOrDefaultAsync(r => r.ReportDate == reportDate && r.CashierId == cashierId);

            var minUtc = reportDate.AddDays(-1);
            var maxUtc = reportDate.AddDays(2);
            var payments = await _context.Payments
                .AsNoTracking()
                .Where(p => p.CashierId == cashierId && p.PaymentTime >= minUtc && p.PaymentTime < maxUtc)
                .ToListAsync();
            var filtered = payments.Where(p => p.PaymentTime.ToLocalTime().Date == reportDate).ToList();

            var cashTotal = filtered.Where(p => p.PaymentMethod == "Cash").Sum(p => p.TotalAmount);
            var vnpayTotal = filtered.Where(p => p.PaymentMethod == "VNPay").Sum(p => p.TotalAmount);

            if (existing != null)
            {
                existing.IsConfirmed = true;
                existing.ConfirmedBy = confirmedByUserId;
                existing.ConfirmedAt = DateTime.UtcNow;
                existing.Note = note;
                existing.ExpectedCashTotal = cashTotal;
                existing.ExpectedVNPayTotal = vnpayTotal;
                existing.TransactionCount = filtered.Count;
            }
            else
            {
                _context.CashierReconciliations.Add(new SmartClinic.Models.Entites.CashierReconciliation
                {
                    ReportDate = reportDate,
                    CashierId = cashierId,
                    ExpectedCashTotal = cashTotal,
                    ExpectedVNPayTotal = vnpayTotal,
                    TransactionCount = filtered.Count,
                    IsConfirmed = true,
                    ConfirmedBy = confirmedByUserId,
                    ConfirmedAt = DateTime.UtcNow,
                    Note = note
                });
            }

            await _context.SaveChangesAsync();
            return (true, "");
        }

        // ─── VNPay Transaction Number (instance field for callback) ──────────────
        private string? _vnpTxnNo;

        // ─── Helper ───────────────────────────────────────────────────────────────

        private static PrescriptionQueueDto MapToDto(QueueTicket t)
        {
            // ─── SOURCE OF TRUTH: Read total amount from QueueTicket ───
            decimal totalAmount = t.TotalAmount ?? 0m;
            decimal consultationFee = 300000m;
            decimal labTestAmount = t.LabOrders.SelectMany(lo => lo.LabOrderDetails).Sum(lod => lod.UnitPrice);
            decimal medicineAmount = t.Prescription?.PrescriptionDetails.Sum(d => (d.UnitPrice > 0 ? d.UnitPrice : 0) * d.Quantity) ?? 0m;

            // Fallback calculation for display if TotalAmount hasn't been set yet
            if (totalAmount <= 0)
            {
                totalAmount = consultationFee + medicineAmount + labTestAmount;
            }

            return new PrescriptionQueueDto
            {
                PrescriptionId   = t.Id,
                TicketId         = t.Id,
                TicketNumber     = t.TicketNumber,
                PatientName      = t.PatientUser?.FullName ?? "—",
                DoctorName       = t.Doctor?.FullName ?? "Not assigned",
                DoctorNote       = t.Prescription?.DoctorNote,
                Status           = t.StatusEnum.ToString(),
                StatusDisplay    = GetStatusLabel(t.StatusEnum),
                ConsultationFee  = consultationFee,
                MedicineAmount   = medicineAmount,
                LabTestAmount    = labTestAmount,
                TotalAmount      = totalAmount,
                CreatedAt        = t.UpdatedAt ?? t.CreatedAt,
                Details          = t.Prescription?.PrescriptionDetails.Select(d => new PrescriptionDetailDto
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

        private static string GetStatusLabel(TicketStatus status) => status switch
        {
            TicketStatus.Completed => "Sẵn sàng thanh toán",
            _ => "Chờ khám/xét nghiệm"
        };
    }
}