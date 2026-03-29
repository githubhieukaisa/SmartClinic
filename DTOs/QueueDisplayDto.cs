namespace SmartClinic.DTOs
{
    public class QueueDisplayDto
    {
        public string CurrentTicketNumber { get; set; }
        public string RoomName { get; set; }
        public string PatientName { get; set; } = "Đang chờ bệnh nhân...";
        public string DoctorName { get; set; } = "Đang chờ bác sĩ...";
        public string Specialty { get; set; } = "";
        public string? NextTicketNumber { get; set; }
    }
}
