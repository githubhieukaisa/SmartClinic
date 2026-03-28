namespace SmartClinic.Constant
{
    public enum TicketStatus : byte
    {
        Waiting = 0,
        Calling = 1,
        Missed = 2,
        Examinating = 3,
        Completed = 4,
        Cancelled = 5,
        Done = 6,
        Testing = 7,
        Appointment = 8
    }
}