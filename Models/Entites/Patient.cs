using SmartClinic.Constant;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartClinic.Models;

public partial class Patient : BaseEntity
{
    public string FullName { get; set; } = null!;

    public DateOnly? DoB { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }
    public PatientFlags Flags { get; set; } = PatientFlags.None;

    public virtual ICollection<QueueTicket> QueueTickets { get; set; } = new List<QueueTicket>();

    [NotMapped]
    public bool Gender
    {
        get => Flags.HasFlag(PatientFlags.Male);

        set
        {
            if (value)
                Flags |= PatientFlags.Male;
            else
                Flags &= ~PatientFlags.Male;
        }
    }

    [NotMapped]
    public bool IsDelete
    {
        get => Flags.HasFlag(PatientFlags.IsDeleted);
        set
        {
            if (value)
                Flags |= PatientFlags.IsDeleted;
            else
                Flags &= ~PatientFlags.IsDeleted;
        }
    }
}
