namespace SmartClinic.Constant
{
    public enum DoctorShiftStatus : byte
    {
        Draft = 0,      // Manager đã phân, BS chưa kích hoạt
        Active = 1,     // Đã kích hoạt, đang nhận bệnh
        Completed = 2,  // Ca đã kết thúc hoàn toàn

        Closing = 3 // Đã dừng nhận bệnh mới, vẫn đang khám dứt điểm hàng đợi
    }
}
