using SmartClinic.Constant;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartClinic.Models
{
    public class Room : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public RoomFlags Flags { get; set; } = RoomFlags.IsActive;

        public int DepartmentId { get; set; }
        public virtual Department Department { get; set; } = null!;

        public virtual ICollection<QueueTicket> QueueTickets { get; set; } = new List<QueueTicket>();
        public virtual ICollection<DoctorShift> DoctorShifts { get; set; } = new List<DoctorShift>();

        // Helper properties để dễ sử dụng
        [NotMapped]
        public bool IsActive
        {
            get => (Flags & RoomFlags.IsActive) != 0;
            set => Flags = value ? (Flags | RoomFlags.IsActive) : (Flags & ~RoomFlags.IsActive);
        }

        [NotMapped]
        public bool IsLab
        {
            get => (Flags & RoomFlags.IsLab) != 0;
            set => Flags = value ? (Flags | RoomFlags.IsLab) : (Flags & ~RoomFlags.IsLab);
        }
    }
}

