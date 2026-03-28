namespace SmartClinic.Models
{
    public class ShiftDefinition : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int SortOrder { get; set; }
    }
}
