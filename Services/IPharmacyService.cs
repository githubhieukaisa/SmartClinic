using SmartClinic.DTOs;
using SmartClinic.Models;

namespace SmartClinic.Services
{
    public interface IPharmacyService
    {
        // ── Prescription Queue ──────────────────────────────────────────────────
        Task<List<PrescriptionQueueDto>> GetPendingPrescriptionsAsync();
        Task<PrescriptionQueueDto?> GetPrescriptionDetailAsync(int prescriptionId);
        Task<(bool Success, string ErrorMessage)> DispenseMedicinesAsync(int prescriptionId);
        Task NotifyNewPrescriptionAsync(int prescriptionId);
        Task NotifyPrescriptionDeletedAsync(int prescriptionId);

        // ── Medicine CRUD ───────────────────────────────────────────────────────
        Task<List<Medicine>> GetAllMedicinesAsync();
        Task<Medicine?> GetMedicineByIdAsync(int id);
        Task<Medicine> CreateMedicineAsync(Medicine medicine);
        Task<(bool Success, string Error)> UpdateMedicineAsync(Medicine medicine);
        Task<(bool Success, string Error)> DeleteMedicineAsync(int id);
    }
}