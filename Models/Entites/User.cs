using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class User : BaseEntity
{
    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? FullName { get; set; }

    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public bool? Gender { get; set; }
    public DateOnly? DoB { get; set; }
    public int RoleMask { get; set; }

    // Khoa chuyên môn (chỉ dùng cho bác sĩ, null cho các role khác)
    public int? DepartmentId { get; set; }
    public virtual Department? Department { get; set; }

    public bool? IsActive { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public virtual ICollection<QueueTicket> DoctorTickets { get; set; } = new List<QueueTicket>();
    public virtual ICollection<QueueTicket> PatientTickets { get; set; } = new List<QueueTicket>();
    public virtual ICollection<DoctorShift> DoctorShifts { get; set; } = new List<DoctorShift>();
}
