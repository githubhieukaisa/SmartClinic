using SmartClinic.Constant;

namespace SmartClinic.DTOs
{
    public class ReceptionDashboardDto
    {
        public List<ReceptionAppointmentItemDto> AppointmentTickets { get; set; } = new();
        public List<ReceptionRoomLiveItemDto> LiveRooms { get; set; } = new();
    }

    public class ReceptionAppointmentItemDto
    {
        public int TicketId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public int TicketNumber { get; set; }
        public DateTime AppointmentDate { get; set; }
        public TimeSpan ShiftStartTime { get; set; }
        public TimeSpan ShiftEndTime { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string PatientPhone { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public TicketStatus Status { get; set; }
        public bool CanCheckIn { get; set; }
    }

    public class ReceptionRoomLiveItemDto
    {
        public int DoctorShiftId { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan ShiftStartTime { get; set; }
        public TimeSpan ShiftEndTime { get; set; }
        public int Capacity { get; set; }
        public int RemainCapacity { get; set; }
        public int WaitingCount { get; set; }
        public bool IsActiveNow { get; set; }
        public bool IsLeastBusy { get; set; }
    }

    public class AppointmentCheckInResultDto
    {
        public int TicketId { get; set; }
        public int NewStt { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
    }
}
