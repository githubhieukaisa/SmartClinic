using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class QueueTicket : BaseEntity
{
    public int? PatientId { get; set; }

    public int TicketNumber { get; set; }

    public string Status { get; set; } = null!;

    public string? ClinicalDiagnosis { get; set; }

    public int? DoctorId { get; set; }

    public virtual User? Doctor { get; set; }

    public virtual Patient? Patient { get; set; }

    public virtual Prescription? Prescription { get; set; }
}