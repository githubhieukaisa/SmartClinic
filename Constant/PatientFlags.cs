namespace SmartClinic.Constant
{
    [Flags]
    public enum PatientFlags : short 
    {
        None = 0,         // Mặc định (Nữ, Chưa xóa)
        Male = 1,         // Bit 1: Giá trị 1
        IsDeleted = 2,    // Bit 2: Giá trị 2
    }
}
