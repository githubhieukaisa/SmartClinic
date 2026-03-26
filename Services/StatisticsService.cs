using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
using SmartClinic.DTOs;
using SmartClinic.Models;

namespace SmartClinic.Services;

public interface IStatisticsService
{
    Task<OverviewStats> GetOverviewAsync(DateTime date);
    Task<List<DailyRevenueItem>> GetDailyRevenueAsync(DateTime from, DateTime to);
    Task<List<RevenueBreakdownItem>> GetRevenueBreakdownAsync(DateTime from, DateTime to);
    Task<List<PatientsByDepartmentItem>> GetPatientsByDepartmentAsync(DateTime from, DateTime to);
}

public class StatisticsService : IStatisticsService
{
    private readonly SmartClinicDbContext _context;

    public StatisticsService(SmartClinicDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy 4 KPI cho ngày chỉ định + % thay đổi so với ngày trước đó
    /// </summary>
    public async Task<OverviewStats> GetOverviewAsync(DateTime date)
    {
        var todayStart = date.Date;
        var todayEnd = todayStart.AddDays(1);
        var yesterdayStart = todayStart.AddDays(-1);

        // ── Bệnh nhân (QueueTicket created) ──
        var todayPatients = await _context.QueueTickets
            .CountAsync(t => t.CreatedAt >= todayStart && t.CreatedAt < todayEnd);
        var yesterdayPatients = await _context.QueueTickets
            .CountAsync(t => t.CreatedAt >= yesterdayStart && t.CreatedAt < todayStart);

        // ── Doanh thu (QueueTicket.Done) ──
        var todayRevenue = await _context.QueueTickets
            .Where(t => t.StatusEnum == TicketStatus.Done
                     && t.CreatedAt >= todayStart && t.CreatedAt < todayEnd)
            .SumAsync(t => t.TotalAmount ?? 0);
        var yesterdayRevenue = await _context.QueueTickets
            .Where(t => t.StatusEnum == TicketStatus.Done
                     && t.CreatedAt >= yesterdayStart && t.CreatedAt < todayStart)
            .SumAsync(t => t.TotalAmount ?? 0);

        // ── Đơn thuốc đã phát (Dispensed hoặc Paid) ──
        var todayPrescriptions = await _context.Prescriptions
            .CountAsync(p => (p.Status == PrescriptionStatus.Dispensed || p.Status == PrescriptionStatus.Paid)
                          && p.CreatedAt >= todayStart && p.CreatedAt < todayEnd);
        var yesterdayPrescriptions = await _context.Prescriptions
            .CountAsync(p => (p.Status == PrescriptionStatus.Dispensed || p.Status == PrescriptionStatus.Paid)
                          && p.CreatedAt >= yesterdayStart && p.CreatedAt < todayStart);

        // ── Xét nghiệm hoàn thành ──
        var todayLabs = await _context.LabOrders
            .CountAsync(l => l.Status == LabOrderStatus.Completed
                          && l.CreatedAt >= todayStart && l.CreatedAt < todayEnd);
        var yesterdayLabs = await _context.LabOrders
            .CountAsync(l => l.Status == LabOrderStatus.Completed
                          && l.CreatedAt >= yesterdayStart && l.CreatedAt < todayStart);

        return new OverviewStats
        {
            TodayPatients = todayPatients,
            TodayRevenue = todayRevenue,
            TodayPrescriptions = todayPrescriptions,
            TodayLabOrders = todayLabs,
            PatientChange = CalcChange(yesterdayPatients, todayPatients),
            RevenueChange = CalcChange((double)yesterdayRevenue, (double)todayRevenue),
            PrescriptionChange = CalcChange(yesterdayPrescriptions, todayPrescriptions),
            LabOrderChange = CalcChange(yesterdayLabs, todayLabs),
        };
    }

    /// <summary>
    /// Doanh thu theo từng ngày trong khoảng [from, to]
    /// </summary>
    public async Task<List<DailyRevenueItem>> GetDailyRevenueAsync(DateTime from, DateTime to)
    {
        var fromDate = from.Date;
        var toDate = to.Date.AddDays(1);

        var data = await _context.QueueTickets
            .AsNoTracking()
            .Where(t => t.StatusEnum == TicketStatus.Done
                     && t.CreatedAt >= fromDate && t.CreatedAt < toDate)
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new DailyRevenueItem
            {
                Date = g.Key,
                Revenue = g.Sum(t => t.TotalAmount ?? 0)
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        // Điền ngày trống (revenue = 0) để chart có đầy đủ các ngày
        var result = new List<DailyRevenueItem>();
        for (var d = fromDate; d < toDate; d = d.AddDays(1))
        {
            var existing = data.FirstOrDefault(x => x.Date == d);
            result.Add(existing ?? new DailyRevenueItem { Date = d, Revenue = 0 });
        }

        return result;
    }

    /// <summary>
    /// Cơ cấu doanh thu: Khám / Thuốc / Xét nghiệm
    /// </summary>
    public async Task<List<RevenueBreakdownItem>> GetRevenueBreakdownAsync(DateTime from, DateTime to)
    {
        var fromDate = from.Date;
        var toDate = to.Date.AddDays(1);

        // Tổng doanh thu (Done tickets)
        var totalRevenue = await _context.QueueTickets
            .Where(t => t.StatusEnum == TicketStatus.Done
                     && t.CreatedAt >= fromDate && t.CreatedAt < toDate)
            .SumAsync(t => t.TotalAmount ?? 0);

        // Tiền thuốc = SUM(Quantity * UnitPrice) từ PrescriptionDetail
        var medicineRevenue = await _context.PrescriptionDetails
            .AsNoTracking()
            .Where(d => d.Prescription != null
                     && (d.Prescription.Status == PrescriptionStatus.Dispensed
                         || d.Prescription.Status == PrescriptionStatus.Paid)
                     && d.Prescription.CreatedAt >= fromDate
                     && d.Prescription.CreatedAt < toDate)
            .SumAsync(d => d.Quantity * d.UnitPrice);

        // Tiền xét nghiệm = SUM(UnitPrice) từ LabOrderDetail
        var labRevenue = await _context.LabOrderDetails
            .AsNoTracking()
            .Where(d => d.LabOrder.Status == LabOrderStatus.Completed
                     && d.LabOrder.CreatedAt >= fromDate
                     && d.LabOrder.CreatedAt < toDate)
            .SumAsync(d => d.UnitPrice);

        // Tiền khám = tổng - thuốc - xét nghiệm
        var examRevenue = totalRevenue - medicineRevenue - labRevenue;
        if (examRevenue < 0) examRevenue = 0;

        return new List<RevenueBreakdownItem>
        {
            new() { Category = "Khám bệnh", Amount = examRevenue },
            new() { Category = "Thuốc", Amount = medicineRevenue },
            new() { Category = "Xét nghiệm", Amount = labRevenue },
        };
    }

    /// <summary>
    /// Số lượng bệnh nhân (distinct) theo từng khoa trong khoảng [from, to]
    /// </summary>
    public async Task<List<PatientsByDepartmentItem>> GetPatientsByDepartmentAsync(DateTime from, DateTime to)
    {
        var fromDate = from.Date;
        var toDate = to.Date.AddDays(1);

        var data = await _context.QueueTickets
            .AsNoTracking()
            .Where(t => t.CreatedAt >= fromDate && t.CreatedAt < toDate)
            .Include(t => t.Room)
                .ThenInclude(r => r.Department)
            .GroupBy(t => t.Room.Department.Name)
            .Select(g => new PatientsByDepartmentItem
            {
                DepartmentName = g.Key,
                PatientCount = g.Count()
            })
            .OrderByDescending(x => x.PatientCount)
            .ToListAsync();

        return data;
    }

    // ── Helper: tính % thay đổi ──
    private static double CalcChange(double yesterday, double today)
    {
        if (yesterday == 0) return today > 0 ? 100 : 0;
        return Math.Round((today - yesterday) / yesterday * 100, 1);
    }
}
