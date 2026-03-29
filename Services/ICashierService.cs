using SmartClinic.DTOs;

namespace SmartClinic.Services
{
    public interface ICashierService
    {
        Task<List<PrescriptionQueueDto>> GetDispensedPrescriptionsAsync();
        Task<(bool Success, string ErrorMessage)> ProcessPaymentAsync(ProcessPaymentRequestDto request, int? cashierId = null);

        string CreateVNPayUrl(int prescriptionId, decimal amount, string patientName);

        Task<(bool Success, string ErrorMessage)> HandleVNPayCallbackAsync(IQueryCollection query);

        Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(DateTime date, int? cashierId = null);

        // ── Manager reconciliation ──
        Task<List<CashierDailySummaryDto>> GetDailySummariesAsync(DateTime date);
        Task<(bool Success, string Error)> ConfirmReconciliationAsync(int cashierId, DateTime date, int confirmedByUserId, string? note);
    }
}