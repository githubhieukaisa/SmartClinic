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

    public class CashierDailySummaryDto
    {
        public int CashierId { get; set; }
        public string CashierName { get; set; } = "";
        public DateTime ReportDate { get; set; }
        public int TransactionCount { get; set; }
        public decimal CashTotal { get; set; }
        public decimal VNPayTotal { get; set; }
        public decimal GrandTotal => CashTotal + VNPayTotal;
        public bool IsConfirmed { get; set; }
        public string? ConfirmedByName { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? Note { get; set; }
        public List<PaymentHistoryDto> Details { get; set; } = new();
    }
}
