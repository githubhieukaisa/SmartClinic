using SmartClinic.Models;
using System.Collections.Generic;

namespace SmartClinic.DTOs
{
    public class DoctorDashboardDto
    {
        public int TotalCount { get; set; }
        public int WaitingCount { get; set; }
        public int InProgressCount { get; set; }
        
        public List<QueueTicket> TestingTickets { get; set; } = new();
        public List<QueueTicket> RecentCompletedTickets { get; set; } = new();
    }
}
