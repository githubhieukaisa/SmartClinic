namespace SmartClinic.Models
{
    using SmartClinic.Constant;
    using System.ComponentModel.DataAnnotations.Schema;

    public class DoctorShift
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public virtual User Doctor { get; set; } = null!;

        public int RoomId { get; set; }
        public virtual Room Room { get; set; } = null!;

        public int ShiftDefinitionId { get; set; }
        public virtual ShiftDefinition ShiftDefinition { get; set; } = null!;

        public DateTime Date { get; set; }

        public int Capacity { get; set; }
        public int RemainCapacity { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Trạng thái workflow: Draft → Active → Completed.
        /// Lưu DB. Dùng chung cho Manager lẫn Doctor.
        /// </summary>
        public DoctorShiftStatus StatusEnum { get; set; } = DoctorShiftStatus.Draft;

        /// <summary>
        /// Trạng thái thời gian (real-time, không lưu DB).
        /// Dùng hiển thị ở Manager Scheduling: "Sắp diễn ra" / "Đang trực" / "Đã hoàn thành".
        /// </summary>
        [NotMapped]
        public string ComputedStatus
        {
            get
            {
                // Nếu ca đã kết thúc hoàn toàn thì báo xong
                if (StatusEnum == DoctorShiftStatus.Completed) return "Đã hoàn thành";

                if (ShiftDefinition == null) return "Sắp diễn ra";
                var now = DateTime.Now;
                var startDateTime = Date.Date.Add(ShiftDefinition.StartTime);
                var endDateTime = Date.Date.Add(ShiftDefinition.EndTime);

                if (now < startDateTime) return "Sắp diễn ra";
                if (now > endDateTime) return "Đã hoàn thành";
                return "Đang trực";
            }
        }

        /// <summary>
        /// Hiển thị tiếng Việt cho StatusEnum. Dùng cho UI.
        /// </summary>
        [NotMapped]
        public string StatusDisplay => StatusEnum switch
        {
            DoctorShiftStatus.Active => "Đang nhận bệnh",
            DoctorShiftStatus.Closing => "Đang chốt sổ",
            DoctorShiftStatus.Completed => "Đã hoàn thành",
            _ => "Chờ kích hoạt"
        };
    }
}
