namespace SmartClinic.Models;

public class DoctorEvaluation
{
    /// <summary>
    /// Guid làm khóa chính, đồng thời là Token an toàn trong URL công khai.
    /// Ví dụ: /feedback/3fa85f64-5717-4562-b3fc-2c963f66afa6
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public int QueueTicketId { get; set; }
    public virtual QueueTicket QueueTicket { get; set; } = null!;

    public int? PatientId { get; set; }
    public virtual User? Patient { get; set; }

    public int? DoctorId { get; set; }
    public virtual User? Doctor { get; set; }

    /// <summary>Điểm đánh giá từ 1 đến 5.</summary>
    public int? Rating { get; set; }

    public string? Comment { get; set; }

    /// <summary>False = Chưa đánh giá, True = Đã nộp.</summary>
    public bool IsSubmitted { get; set; } = false;

    public DateTime? SubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
