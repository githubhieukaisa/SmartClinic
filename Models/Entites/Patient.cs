using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class Patient : BaseEntity
{
    public string FullName { get; set; } = null!;

    public DateOnly? DoB { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public virtual ICollection<QueueTicket> QueueTickets { get; set; } = new List<QueueTicket>();
}
