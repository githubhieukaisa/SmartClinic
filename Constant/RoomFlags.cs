namespace SmartClinic.Constant
{
    [Flags]
    public enum RoomFlags : byte
    {
        None = 0,        // M?c ??nh (Inactive, Not Lab)
        IsActive = 1,    // Bit 1: Gi� tr? 1
        IsLab = 2,       // Bit 2: Gi� tr? 2
    }
}
