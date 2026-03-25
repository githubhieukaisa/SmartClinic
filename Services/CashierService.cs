using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.DTOs;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    /// <summary>
    /// Service xử lý nghiệp vụ Thu ngân:
    /// - Lấy danh sách đơn chờ thanh toán
    /// - Xác nhận thanh toán, kết thúc quy trình
    ///
    /// Registered as Scoped.
    /// </summary>
    public class CashierService : ICashierService
    {
        private readonly SmartClinicDbContext _context;
        private readonly IHubContext<PrescriptionHub> _hubContext;

        public CashierService(SmartClinicDbContext context, IHubContext<PrescriptionHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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
                .Where(p => p.Status == PrescriptionStatus.Dispensed)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return prescriptions.Select(MapToDto).ToList();
        }

        public async Task<(bool Success, string ErrorMessage)> ProcessPaymentAsync(int prescriptionId)
        {
            System.Diagnostics.Debug.WriteLine($"[CashierService] Processing payment for prescription {prescriptionId}");

            try
            {
                var prescription = await _context.Prescriptions
                    .Include(p => p.Ticket)
                        .ThenInclude(t => t!.Patient)
                    .FirstOrDefaultAsync(p => p.Id == prescriptionId);

                if (prescription == null)
                    return (false, "Không tìm thấy đơn thuốc.");

                if (prescription.Status != PrescriptionStatus.Dispensed)
                    return (false, $"Đơn thuốc đang ở trạng thái '{prescription.Status}', chưa thể thanh toán.");

                // 1. Đánh dấu đã thanh toán
                prescription.Status = PrescriptionStatus.Paid;

                // 2. Kết thúc queue ticket
                if (prescription.Ticket != null)
                {
                    prescription.Ticket.Status = "Done";
                }

                await _context.SaveChangesAsync();

                // 3. Thông báo cho tất cả (bác sĩ / màn hình chờ cập nhật trạng thái)
                await _hubContext.Clients.All.SendAsync("PaymentCompleted", new
                {
                    prescriptionId,
                    ticketNumber = prescription.Ticket?.TicketNumber,
                    patientName = prescription.Ticket?.Patient?.FullName
                });

                System.Diagnostics.Debug.WriteLine($"[CashierService] Payment OK for prescription {prescriptionId}");
                return (true, "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CashierService] ERROR: {ex.Message}");
                return (false, "Lỗi hệ thống khi xử lý thanh toán.");
            }
        }

        private static PrescriptionQueueDto MapToDto(Prescription p) => new()
        {
            PrescriptionId = p.Id,
            TicketId = p.TicketId ?? 0,
            TicketNumber = p.Ticket?.TicketNumber ?? 0,
            PatientName = p.Ticket?.Patient?.FullName ?? "—",
            DoctorName = p.Ticket?.Doctor?.FullName ?? "—",
            DoctorNote = p.DoctorNote,
            Status = p.Status.ToString(),
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