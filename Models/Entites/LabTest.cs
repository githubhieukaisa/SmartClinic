using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public class LabTest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Unit { get; set; }
    public string? Description { get; set; }

    public int? DefaultRoomId { get; set; }
    public virtual Room? DefaultRoom { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<LabOrderDetail> LabOrderDetails { get; set; } = new List<LabOrderDetail>();
    public virtual ICollection<LabPrice> LabPrices { get; set; } = new List<LabPrice>();
}
