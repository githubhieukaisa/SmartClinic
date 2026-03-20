using SmartClinic.DTOs;

namespace SmartClinic.Services
{
    public interface ICashierService
    {
        /// <summary>Lấy danh sách đơn đã xuất thuốc, chờ thanh toán (Status = "Dispensed")</summary>
        Task<List<PrescriptionQueueDto>> GetDispensedPrescriptionsAsync();

        /// <summary>
        /// Thu ngân xác nhận thanh toán:
        /// 1. Prescription.Status = "Paid"
        /// 2. QueueTicket.Status = "Done"
        /// 3. Broadcast SignalR "PaymentCompleted" toàn bộ (optional)
        /// </summary>
        Task<(bool Success, string ErrorMessage)> ProcessPaymentAsync(int prescriptionId);
    }
}