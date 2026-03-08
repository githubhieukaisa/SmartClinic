using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class Medicine : BaseEntity
{
    public string Name { get; set; } = null!;

    public string? Unit { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public virtual ICollection<PrescriptionDetail> PrescriptionDetails { get; set; } = new List<PrescriptionDetail>();
}
