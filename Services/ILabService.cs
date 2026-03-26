using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartClinic.Models;

namespace SmartClinic.Services;

public interface ILabService
{
    Task<List<LabTestDto>> GetAllLabTestsAsync();
    Task CreateLabOrderAsync(int ticketId, List<int> labTestIds);
    Task<List<LabOrder>> GetPendingLabOrdersAsync();
    Task<List<LabOrder>> GetTodayLabOrdersAsync();
    Task<List<Room>> GetLabStationsAsync();
    Task SubmitLabResultAsync(int labOrderDetailId, string resultNotes, string? resultFileUrl, int? performedId = null, string? performedBy = null);
    Task<bool> HasPendingLabOrdersAsync(int ticketId);
    Task<List<LabOrder>> GetLabOrdersByTicketAsync(int ticketId);
    Task<string> UploadFileAsync(IBrowserFile file);
    Task<decimal?> GetCurrentLabTestPriceAsync(int labTestId);
}
