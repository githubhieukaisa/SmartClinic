using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class LabOrder : BaseEntity
{
    public int TicketId { get; set; }
    
    // Status can be: "Pending", "Done"
    public string Status { get; set; } = "Pending";

    public virtual QueueTicket QueueTicket { get; set; } = null!;
    public virtual ICollection<LabOrderDetail> LabOrderDetails { get; set; } = new List<LabOrderDetail>();
}
