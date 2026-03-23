using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class LabTest : BaseEntity
{
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }

    public int? DefaultRoomId { get; set; }
    public virtual Room? DefaultRoom { get; set; }

    public virtual ICollection<LabOrderDetail> LabOrderDetails { get; set; } = new List<LabOrderDetail>();
}
