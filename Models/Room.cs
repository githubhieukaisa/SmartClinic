namespace SmartClinic.Models
{
    public class Room : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        public int DepartmentId { get; set; }
        public virtual Department Department { get; set; } = null!;

        public virtual ICollection<User> Doctors { get; set; } = new List<User>();
        public virtual ICollection<QueueTicket> QueueTickets { get; set; } = new List<QueueTicket>();
    }
}
