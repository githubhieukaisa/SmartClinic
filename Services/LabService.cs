using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Constant;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services;

public class LabService : ILabService
{
    private readonly IDbContextFactory<SmartClinicDbContext> _dbFactory;
    private readonly IHubContext<LabHub> _labHubContext;
    private readonly IWebHostEnvironment _environment;

    public LabService(IDbContextFactory<SmartClinicDbContext> dbFactory, IHubContext<LabHub> labHubContext, IWebHostEnvironment environment)
    {
        _dbFactory = dbFactory;
        _labHubContext = labHubContext;
        _environment = environment;
    }

    public async Task<List<LabTest>> GetAllLabTestsAsync()
    {
        using var context = _dbFactory.CreateDbContext();
        return await context.LabTests
            .Include(t => t.DefaultRoom)
            .ToListAsync();
    }

    public async Task CreateLabOrderAsync(int ticketId, List<int> labTestIds)
    {
        using var context = _dbFactory.CreateDbContext();
        var ticket = await context.QueueTickets.FindAsync(ticketId);
        if (ticket == null) return;

        var labOrder = new LabOrder
        {
            TicketId = ticketId,
            Status = "Pending"
        };
        context.LabOrders.Add(labOrder);
        await context.SaveChangesAsync(); 

        var labTests = await context.LabTests.Where(lt => labTestIds.Contains(lt.Id)).ToListAsync();
        foreach (var test in labTests)
        {
            var detail = new LabOrderDetail
            {
                LabOrderId = labOrder.Id,
                LabTestId = test.Id,
                UnitPrice = test.Price
            };
            context.LabOrderDetails.Add(detail);
        }

        ticket.StatusEnum = TicketStatus.Testing;
        await context.SaveChangesAsync();

        await _labHubContext.Clients.Group("LabTechnicians").SendAsync("LabOrderCreated", labOrder.Id);
    }


    public async Task<List<LabOrder>> GetPendingLabOrdersAsync()
    {
        using var context = _dbFactory.CreateDbContext();
        return await context.LabOrders
            .Include(lo => lo.QueueTicket)
            .ThenInclude(qt => qt.Patient)
            .Include(lo => lo.LabOrderDetails)
            .ThenInclude(lod => lod.LabTest)
            .Where(lo => lo.Status == "Pending")
            .OrderBy(lo => lo.CreatedAt)
            .ToListAsync();
    }

    public async Task SubmitLabResultAsync(int labOrderDetailId, string resultNotes, string? resultFileUrl)
    {
        using var context = _dbFactory.CreateDbContext();
        var detail = await context.LabOrderDetails
            .Include(lod => lod.LabOrder)
            .ThenInclude(lo => lo.QueueTicket)
            .FirstOrDefaultAsync(lod => lod.Id == labOrderDetailId);

        if (detail == null) return;

        detail.ResultNotes = resultNotes;
        detail.ResultFileUrl = resultFileUrl;
        await context.SaveChangesAsync();

        // Broadcast to Doctor immediately so they can see partial results
        if (detail.LabOrder.QueueTicket != null)
        {
            string doctorRoomGroup = $"DoctorRoom_{detail.LabOrder.QueueTicket.RoomId}";
            await _labHubContext.Clients.Group(doctorRoomGroup).SendAsync("LabResultReady", detail.LabOrder.TicketId);
        }

        var orderDetails = await context.LabOrderDetails
            .Where(lod => lod.LabOrderId == detail.LabOrderId)
            .ToListAsync();

        // Check if all details in this order are done
        bool allDetailsDone = orderDetails.All(lod => !string.IsNullOrEmpty(lod.ResultNotes));
        if (allDetailsDone)
        {
            detail.LabOrder.Status = "Done";
            await context.SaveChangesAsync();

            var allOrdersForTicket = await context.LabOrders
                .Where(lo => lo.TicketId == detail.LabOrder.TicketId)
                .ToListAsync();

            // Check if all lab orders for this patient are done
            bool allOrdersDone = allOrdersForTicket.All(lo => lo.Status == "Done");
            if (allOrdersDone)
            {
                detail.LabOrder.QueueTicket.StatusEnum = TicketStatus.Examinating;
                await context.SaveChangesAsync();
            }
        }
    }

    public async Task<bool> HasPendingLabOrdersAsync(int ticketId)
    {
        using var context = _dbFactory.CreateDbContext();
        return await context.LabOrders
            .AnyAsync(lo => lo.TicketId == ticketId && lo.Status != "Done");
    }

    public async Task<List<LabOrder>> GetLabOrdersByTicketAsync(int ticketId)
    {
        using var context = _dbFactory.CreateDbContext();
        return await context.LabOrders
            .Include(lo => lo.LabOrderDetails)
                .ThenInclude(lod => lod.LabTest)
            .Where(lo => lo.TicketId == ticketId)
            .OrderByDescending(lo => lo.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<LabOrder>> GetTodayLabOrdersAsync()
    {
        using var context = _dbFactory.CreateDbContext();
        var today = System.DateTime.Today;
        return await context.LabOrders
            .Include(lo => lo.QueueTicket)
                .ThenInclude(qt => qt.Patient)
            .Include(lo => lo.LabOrderDetails)
                .ThenInclude(lod => lod.LabTest)
            .Where(lo => lo.CreatedAt >= today)
            .OrderBy(lo => lo.CreatedAt)
            .ToListAsync();
    }
    public async Task<List<Room>> GetLabStationsAsync()
    {
        using var context = _dbFactory.CreateDbContext();
        return await context.LabTests
            .Where(t => t.DefaultRoomId != null)
            .Select(t => t.DefaultRoom!)
            .Distinct()
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<string> UploadFileAsync(IBrowserFile file)
    {
        try
        {
            var folder = Path.Combine(_environment.WebRootPath, "uploads", "lab-results");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var fileName = $"{System.Guid.NewGuid()}_{file.Name}";
            var path = Path.Combine(folder, fileName);

            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10MB max
            using var fileStream = new FileStream(path, FileMode.Create);
            await stream.CopyToAsync(fileStream);

            return $"/uploads/lab-results/{fileName}";
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [LabService] Upload error: {ex.Message}");
            throw;
        }
    }
}
