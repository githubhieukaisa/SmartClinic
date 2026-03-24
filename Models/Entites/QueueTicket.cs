using System;
using System.Collections.Generic;
using SmartClinic.Constant;

namespace SmartClinic.Models;

public partial class QueueTicket : BaseEntity
{
    public int? PatientId { get; set; }

    public int TicketNumber { get; set; }

    public string Status { get; set; } = null!;

    public TicketStatus StatusEnum { get; set; } = TicketStatus.Waiting;

    public string? ClinicalDiagnosis { get; set; }
    public decimal? TotalAmount { get; set; }
    public int MissCount { get; set; } = 0;

    public int? DoctorId { get; set; }

    public virtual User? Doctor { get; set; }

    public virtual Patient? Patient { get; set; }

    public int RoomId { get; set; }
    public virtual Room Room { get; set; } = null!;

    public virtual Prescription? Prescription { get; set; }
    public string? Diagnosis { get; set; }
    public string? TreatmentPlan { get; set; }
    public string? AdditionalNotes { get; set; }

    public virtual ICollection<LabOrder> LabOrders { get; set; } = new List<LabOrder>();
}