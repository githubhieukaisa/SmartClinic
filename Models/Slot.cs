namespace SmartClinic.Models
{
    public class Slot : BaseEntity
    {
        public int DoctorShiftId { get; set; }
        public virtual DoctorShift DoctorShift { get; set; } = null!;

        public int SlotNumber { get; set; }

        public bool IsBooked { get; set; } = false;

        public int? PatientId { get; set; }
        public virtual Patient? Patient { get; set; }
    }
}
