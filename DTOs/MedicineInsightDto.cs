namespace SmartClinic.DTOs
{
    public class MedicineInsightDto
    {
        public int MedicineId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public bool IsForSale { get; set; }
    }
}
