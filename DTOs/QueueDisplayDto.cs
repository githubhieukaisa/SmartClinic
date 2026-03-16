namespace SmartClinic.DTOs
{
    public class QueueDisplayDto
    {
        public string CurrentTicketNumber { get; set; }
        public string RoomName { get; set; }
        public string DoctorName { get; set; } = "Đang chờ bác sĩ...";
        public string Specialty { get; set; } = "";
        public List<string> NextTickets { get; set; } = new();
    }
}
