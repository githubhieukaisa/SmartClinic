using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartClinic.Models.Entites
{
    /// <summary>
    /// Bản ghi đối soát thu ngân theo ngày.
    /// Manager/Admin xác nhận rằng Cashier đã nộp đúng số tiền.
    /// </summary>
    public class CashierReconciliation : BaseEntity
    {
        public DateTime ReportDate { get; set; }

        public int CashierId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpectedCashTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpectedVNPayTotal { get; set; }

        public int TransactionCount { get; set; }

        public bool IsConfirmed { get; set; }

        public int? ConfirmedBy { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public string? Note { get; set; }

        [ForeignKey("CashierId")]
        public virtual User Cashier { get; set; } = null!;

        [ForeignKey("ConfirmedBy")]
        public virtual User? ConfirmedByUser { get; set; }
    }
}
