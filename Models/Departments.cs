namespace SmartClinic.Models
{
    public class Department : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
