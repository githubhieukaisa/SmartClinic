using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class PrescriptionDetail : BaseEntity
{

    public int? PrescriptionId { get; set; }

    public int? MedicineId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string? UsageInstruction { get; set; }

    public virtual Medicine? Medicine { get; set; }

    public virtual Prescription? Prescription { get; set; }
}
