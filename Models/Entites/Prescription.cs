using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class Prescription : BaseEntity
{
    public int? TicketId { get; set; }

    public string? DoctorNote { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<PrescriptionDetail> PrescriptionDetails { get; set; } = new List<PrescriptionDetail>();

    public virtual QueueTicket? Ticket { get; set; }
}
