using System;
using System.Collections.Generic;

namespace SmartClinic.Models;

public partial class Medicine : BaseEntity
{
    public string Name { get; set; } = null!;

    public string? Unit { get; set; }

    /// <summary>
    /// Signed stock: >= 0 = đang bán (số lượng = StockQuantity).
    /// < 0 = không bán (số lượng vật lý = ~StockQuantity, tức là bitwise NOT).
    /// Toggle: StockQuantity = ~StockQuantity.
    /// </summary>
    public int StockQuantity { get; set; }

    // ── Computed (không map vào DB) ────────────────────────────────────────────

    /// <summary>true nếu đang bán (StockQuantity >= 0)</summary>
    public bool IsForSale => StockQuantity >= 0;

    /// <summary>Số lượng vật lý thực tế, không phân biệt trạng thái bán</summary>
    public int PhysicalStock => StockQuantity >= 0 ? StockQuantity : ~StockQuantity;

    // ── Navigations ────────────────────────────────────────────────────────────

    public virtual ICollection<MedicinePrice> MedicinePrices { get; set; } = new List<MedicinePrice>();

    public virtual ICollection<PrescriptionDetail> PrescriptionDetails { get; set; } = new List<PrescriptionDetail>();
}
