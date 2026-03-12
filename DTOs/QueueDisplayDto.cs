namespace SmartClinic.DTOs
{
    public class QueueDisplayDto
    {
        public string CurrentTicketNumber { get; set; }
        public string RoomName { get; set; }
        public List<string> NextTickets { get; set; } = new();
    }
}
