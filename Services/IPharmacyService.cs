using SmartClinic.DTOs;

namespace SmartClinic.Services
{
    public interface IPharmacyService
    {
        /// <summary>Lấy danh sách đơn thuốc đang chờ dược sĩ xử lý (Status = "Pending")</summary>
        Task<List<PrescriptionQueueDto>> GetPendingPrescriptionsAsync();

        /// <summary>Lấy chi tiết một đơn thuốc</summary>
        Task<PrescriptionQueueDto?> GetPrescriptionDetailAsync(int prescriptionId);

        /// <summary>
        /// Dược sĩ xuất thuốc:
        /// 1. Trừ StockQuantity từng thuốc
        /// 2. Đổi Prescription.Status = "Dispensed"
        /// 3. Broadcast SignalR "PrescriptionDispensed" cho Thu ngân
        /// </summary>
        Task<(bool Success, string ErrorMessage)> DispenseMedicinesAsync(int prescriptionId);

        /// <summary>Bác sĩ lưu đơn thuốc từ trang Examination - gọi sau khi lưu vào DB</summary>
        Task NotifyNewPrescriptionAsync(int prescriptionId);
    }
}