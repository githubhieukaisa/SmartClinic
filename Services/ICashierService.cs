using SmartClinic.DTOs;

namespace SmartClinic.Services
{
    public interface ICashierService
    {
        Task<List<PrescriptionQueueDto>> GetDispensedPrescriptionsAsync();

        Task<(bool Success, string ErrorMessage)> ProcessPaymentAsync(int prescriptionId);

        string CreateVNPayUrl(int prescriptionId, decimal amount, string patientName);

        Task<(bool Success, string ErrorMessage)> HandleVNPayCallbackAsync(IQueryCollection query);
    }
}