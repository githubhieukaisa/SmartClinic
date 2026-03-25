namespace SmartClinic.Models;

public class MedicinePrice : BaseEntity
{
    public int MedicineId { get; set; }

    public decimal Price { get; set; }

    /// <summary>Ngày giá này có hiệu lực (dùng để lấy giá mới nhất)</summary>
    public DateTime EffectiveFrom { get; set; }

    public virtual Medicine? Medicine { get; set; }
}
