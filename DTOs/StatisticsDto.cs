namespace SmartClinic.DTOs;

/// <summary>
/// 4 KPI cards trên dashboard
/// </summary>
public class OverviewStats
{
    public int TodayPatients { get; set; }
    public decimal TodayRevenue { get; set; }
    public int TodayPrescriptions { get; set; }
    public int TodayLabOrders { get; set; }

    // % thay đổi so với hôm qua
    public double PatientChange { get; set; }
    public double RevenueChange { get; set; }
    public double PrescriptionChange { get; set; }
    public double LabOrderChange { get; set; }
}

/// <summary>
/// Doanh thu theo ngày — dùng cho line/bar chart
/// </summary>
public class DailyRevenueItem
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
}

/// <summary>
/// Cơ cấu doanh thu — dùng cho pie chart
/// </summary>
public class RevenueBreakdownItem
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// Số lượng bệnh nhân theo từng khoa — dùng cho bar chart
/// </summary>
public class PatientsByDepartmentItem
{
    public string DepartmentName { get; set; } = string.Empty;
    public int PatientCount { get; set; }
}
