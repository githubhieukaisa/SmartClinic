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
    public int RoleMask { get; set; }

    public bool? IsActive { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public virtual ICollection<QueueTicket> QueueTickets { get; set; } = new List<QueueTicket>();
    public virtual ICollection<DoctorShift> DoctorShifts { get; set; } = new List<DoctorShift>();
}
