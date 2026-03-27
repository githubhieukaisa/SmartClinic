namespace SmartClinic.Models
{
    using SmartClinic.Constant;

    public class DoctorShift
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public virtual User Doctor { get; set; } = null!;

        public int RoomId { get; set; }
        public virtual Room Room { get; set; } = null!;

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; } // Null nghĩa là ca trực đang diễn ra

        // Trạng thái: "Active" (Đang trực), "Completed" (Đã nghỉ)
        public DoctorShiftStatus StatusEnum { get; set; } = DoctorShiftStatus.Active;
    }
}