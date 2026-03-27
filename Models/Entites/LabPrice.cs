using System;

namespace SmartClinic.Models;

public class LabPrice
{
    public int Id { get; set; }
    public int LabTestId { get; set; }
    public decimal Price { get; set; }
    public DateTime EffectiveDate { get; set; }

    public virtual LabTest LabTest { get; set; } = null!;
}
