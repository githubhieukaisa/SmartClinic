namespace SmartClinic.Constant
{
    [Flags]
    public enum RoomFlags : byte
    {
        None = 0,        // M?c ??nh (Inactive, Not Lab)
        IsActive = 1,    // Bit 1: Giá tr? 1
        IsLab = 2,       // Bit 2: Giá tr? 2
    }
}
