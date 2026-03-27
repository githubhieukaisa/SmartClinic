using System;
using System.Collections.Generic;
using SmartClinic.Constant;

namespace SmartClinic.Models;

public partial class QueueTicket : BaseEntity
{
    public int TicketNumber { get; set; }

    public TicketStatus StatusEnum { get; set; } = TicketStatus.Waiting;

    public decimal? TotalAmount { get; set; }
    public int MissCount { get; set; } = 0;

    public int? DoctorId { get; set; }

    public int? CreatedBy { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? Doctor { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual User? UpdatedByUser { get; set; }

    public int RoomId { get; set; }
    public virtual Room Room { get; set; } = null!;

    public virtual Prescription? Prescription { get; set; }
    public string? Diagnosis { get; set; }
    public string? TreatmentPlan { get; set; }
    public string? AdditionalNotes { get; set; }

    public int? PatientId { get; set; }
    public virtual User? PatientUser { get; set; }

    public virtual ICollection<LabOrder> LabOrders { get; set; } = new List<LabOrder>();
    public virtual HistoryAccess? HistoryAccess { get; set; }
    public virtual DoctorEvaluation? Evaluation { get; set; }
}