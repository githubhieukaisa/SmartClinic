using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class LabOrder : BaseEntity
{
    public int TicketId { get; set; }

    public LabOrderStatus Status { get; set; } = LabOrderStatus.Pending;


    public virtual QueueTicket QueueTicket { get; set; } = null!;
    public virtual ICollection<LabOrderDetail> LabOrderDetails { get; set; } = new List<LabOrderDetail>();
}
