namespace SmartClinic.Models;

public enum PrescriptionStatus : byte
{
    Pending = 0,
    Dispensed = 1,
    Cancelled = 2,
    Paid = 3
}
