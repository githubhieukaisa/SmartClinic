namespace SmartClinic.Models;

public enum LabOrderStatus : byte
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}
