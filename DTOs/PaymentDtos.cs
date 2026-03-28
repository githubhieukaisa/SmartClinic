using System;

namespace SmartClinic.DTOs
{
    public class ProcessPaymentRequestDto
    {
        public int TicketId { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public decimal AmountReceived { get; set; }
    }

    public class PaymentHistoryDto
    {
        public int PaymentId { get; set; }
        public int TicketId { get; set; }
        public string PatientName { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public decimal AmountReceived { get; set; }
        public decimal ChangeAmount { get; set; }
        public string Status { get; set; } = "";
        public DateTime PaymentTime { get; set; }
        public string CashierName { get; set; } = "";
    }
}
