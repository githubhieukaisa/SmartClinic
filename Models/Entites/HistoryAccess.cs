using System;

namespace SmartClinic.Models;

public partial class HistoryAccess
{
    public int Id { get; set; } // Đã đổi từ Guid sang int (Đồng bộ với các bảng khác)
    
    // Khóa ngoại 1-1 với QueueTicket
    public int QueueTicketId { get; set; } 

    public bool IsUnlocked { get; set; } = false;
    public DateTime? UnlockedAt { get; set; }

    public virtual QueueTicket QueueTicket { get; set; } = null!;
}
