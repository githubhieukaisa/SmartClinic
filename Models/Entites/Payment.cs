using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartClinic.Models.Entites
{
    public class Payment : BaseEntity
    {
        public int TicketId { get; set; }
        
        [ForeignKey("TicketId")]
        public virtual QueueTicket? Ticket { get; set; }

        public string PaymentMethod { get; set; } = "Cash"; // Cash, VNPay

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } // Tổng tiền phải thu

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountReceived { get; set; } // Số tiền khách đưa

        [Column(TypeName = "decimal(18,2)")]
        public decimal ChangeAmount { get; set; } // Tiền thối lại

        public int? CashierId { get; set; }
        
        [ForeignKey("CashierId")]
        public virtual User? Cashier { get; set; }
        
        public DateTime PaymentTime { get; set; } = DateTime.UtcNow;
        
        public string Status { get; set; } = "Success"; // Success, Failed, Reflexed
        
        public string? Note { get; set; } // Ghi chú thêm
        
        public string? VnpTransactionNo { get; set; } // Mã giao dịch VNPay (audit trail)
        public string? IpAddress { get; set; }         // IP nguồn thanh toán (audit trail)
    }
}
