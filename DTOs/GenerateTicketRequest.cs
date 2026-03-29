namespace SmartClinic.DTOs
{
    public class GenerateTicketRequest
    {
        public string PatientName { get; set; } = string.Empty;
        public string? PatientPhone { get; set; }
        public int DepartmentId { get; set; }
        public bool PatientGender { get; set; } = true; // true = Nam, false = Nữ
        public int? UserId { get; set; }
        public Constant.TicketStatus StatusEnum { get; set; } = Constant.TicketStatus.Waiting;
    }
}
