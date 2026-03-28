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
    }
}