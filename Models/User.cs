using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class User : BaseEntity
{
    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? FullName { get; set; }

    public int RoleMask { get; set; }

    public bool? IsActive { get; set; }
    public int? RoomId { get; set; }
    public virtual Room? Room { get; set; }
    public virtual ICollection<QueueTicket> QueueTickets { get; set; } = new List<QueueTicket>();
}
