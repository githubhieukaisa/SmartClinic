using System;

namespace SmartClinic.Models;

// Note: Does not inherit from BaseEntity since it doesn't need CreatedAt
public partial class LabOrderDetail
{
    public int Id { get; set; }
    
    public int LabOrderId { get; set; }
    public int LabTestId { get; set; }
    
    public decimal UnitPrice { get; set; }
    
    public string? ResultNotes { get; set; }
    public string? ResultFileUrl { get; set; }

    public virtual LabOrder LabOrder { get; set; } = null!;
    public virtual LabTest LabTest { get; set; } = null!;
}
