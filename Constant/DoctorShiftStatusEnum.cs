namespace SmartClinic.Constant
{
    public enum DoctorShiftStatus : byte
    {
        Draft = 0,      // Manager đã phân, BS chưa kích hoạt
        Active = 1,     // Đã kích hoạt, public cho booking
        Completed = 2   // Ca đã kết thúc
    }
}
