namespace SmartClinic.DTOs
{
    /// <summary>
    /// DTO hiển thị đơn thuốc cho Dược sĩ
    /// </summary>
    public class PrescriptionQueueDto
    {
        public int PrescriptionId { get; set; }
        public int TicketId { get; set; }
        public int TicketNumber { get; set; }
        public string PatientName { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public string? DoctorNote { get; set; }
        public string Status { get; set; } = "Pending";    // Pending | Dispensed | Paid
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PrescriptionDetailDto> Details { get; set; } = new();
    }

    /// <summary>
    /// Chi tiết từng thuốc trong đơn
    /// </summary>
    public class PrescriptionDetailDto
    {
        public int DetailId { get; set; }
        public int MedicineId { get; set; }
        public string MedicineName { get; set; } = "";
        public string? Unit { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal => Quantity * UnitPrice;
        public string? UsageInstruction { get; set; }
    }

    /// <summary>
    /// Payload SignalR gửi cho Dược sĩ khi có đơn mới
    /// </summary>
    public class NewPrescriptionNotificationDto
    {
        public int PrescriptionId { get; set; }
        public int TicketNumber { get; set; }
        public string PatientName { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public int MedicineCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    /// <summary>
    /// Payload SignalR gửi cho Thu ngân khi đơn đã được xuất thuốc
    /// </summary>
    public class PrescriptionDispensedNotificationDto
    {
        public int PrescriptionId { get; set; }
        public int TicketNumber { get; set; }
        public string PatientName { get; set; } = "";
        public decimal TotalAmount { get; set; }
    }
}