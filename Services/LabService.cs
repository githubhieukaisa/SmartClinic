using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartClinic.Hubs;
using SmartClinic.Models;

namespace SmartClinic.Services;

public class LabService : ILabService
{
    private readonly SmartClinicDbContext _context;
    private readonly IHubContext<LabHub> _labHubContext;

    public LabService(SmartClinicDbContext context, IHubContext<LabHub> labHubContext)
    {
        _context = context;
        _labHubContext = labHubContext;
    }

    public async Task<List<LabTest>> GetAllLabTestsAsync()
    {
        return await _context.LabTests.ToListAsync();
    }

    public async Task CreateLabOrderAsync(int ticketId, List<int> labTestIds)
    {
        var ticket = await _context.QueueTickets.FindAsync(ticketId);
        if (ticket == null) return;

        var labOrder = new LabOrder
        {
            TicketId = ticketId,
            Status = "Pending"
        };
        _context.LabOrders.Add(labOrder);
        await _context.SaveChangesAsync(); // Lưu LabOrder để sinh Id

        var labTests = await _context.LabTests.Where(lt => labTestIds.Contains(lt.Id)).ToListAsync();
        foreach (var test in labTests)
        {
            var detail = new LabOrderDetail
            {
                LabOrderId = labOrder.Id,
                LabTestId = test.Id,
                UnitPrice = test.Price
            };
            _context.LabOrderDetails.Add(detail);
        }

        ticket.Status = "Testing";
        await _context.SaveChangesAsync();

        // Broadcast to Lab technicians
        await _labHubContext.Clients.Group("LabTechnicians").SendAsync("LabOrderCreated", labOrder.Id);
    }

    public async Task<List<LabOrder>> GetPendingLabOrdersAsync()
    {
        return await _context.LabOrders
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
        var detail = await _context.LabOrderDetails
            .Include(lod => lod.LabOrder)
            .ThenInclude(lo => lo.QueueTicket)
            .FirstOrDefaultAsync(lod => lod.Id == labOrderDetailId);

        if (detail == null) return;

        detail.ResultNotes = resultNotes;
        detail.ResultFileUrl = resultFileUrl;
        await _context.SaveChangesAsync();

        var orderDetails = await _context.LabOrderDetails
            .Where(lod => lod.LabOrderId == detail.LabOrderId)
            .ToListAsync();

        // Check if all details are done
        bool allDetailsDone = orderDetails.All(lod => !string.IsNullOrEmpty(lod.ResultNotes));
        if (allDetailsDone)
        {
            detail.LabOrder.Status = "Done";
            await _context.SaveChangesAsync();

            var allOrdersForTicket = await _context.LabOrders
                .Where(lo => lo.TicketId == detail.LabOrder.TicketId)
                .ToListAsync();

            bool allOrdersDone = allOrdersForTicket.All(lo => lo.Status == "Done");
            if (allOrdersDone)
            {
                detail.LabOrder.QueueTicket.Status = "Examining";
                await _context.SaveChangesAsync();

                // Broadcast to Doctor
                string doctorRoomGroup = $"DoctorRoom_{detail.LabOrder.QueueTicket.RoomId}";
                await _labHubContext.Clients.Group(doctorRoomGroup).SendAsync("LabResultReady", detail.LabOrder.TicketId);
            }
        }
    }

    public async Task<bool> HasPendingLabOrdersAsync(int ticketId)
    {
        return await _context.LabOrders
            .AnyAsync(lo => lo.TicketId == ticketId && lo.Status != "Done");
    }
}
