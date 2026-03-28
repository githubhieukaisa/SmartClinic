namespace SmartClinic.Models
{
    using SmartClinic.Constant;
    using System.ComponentModel.DataAnnotations.Schema;

    public class DoctorShift : BaseEntity
    {
        public int DoctorId { get; set; }
        public virtual User Doctor { get; set; } = null!;

        public int RoomId { get; set; }
        public virtual Room Room { get; set; } = null!;

        public int ShiftDefinitionId { get; set; }
        public virtual ShiftDefinition ShiftDefinition { get; set; } = null!;

        public DateTime Date { get; set; }

        public int Capacity { get; set; }

        public virtual ICollection<Slot> Slots { get; set; } = new List<Slot>();

        [NotMapped]
        public string ComputedStatus
        {
            get
            {
                if (ShiftDefinition == null) return "Sắp diễn ra"; // Fallback nếu chưa load relate
                var now = DateTime.Now;
                var startDateTime = Date.Date.Add(ShiftDefinition.StartTime);
                var endDateTime = Date.Date.Add(ShiftDefinition.EndTime);

                if (now < startDateTime) return "Sắp diễn ra";
                if (now > endDateTime) return "Đã hoàn thành";
                return "Đang trực";
            }
        }
    }
}