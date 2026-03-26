using System.ComponentModel.DataAnnotations;

namespace SmartClinic.DTOs;

/// <summary>
/// Hiển thị ca trực trong bảng danh sách.
/// Map từ DoctorShift entity → flatten ra các tên để hiển thị.
/// </summary>
public class DoctorShiftDisplayDto
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO khi Admin tạo ca trực mới.
/// Chỉ cần 4 thông tin: ai trực, ở phòng nào, từ lúc nào đến lúc nào.
/// </summary>
public class CreateShiftDto
{
    [Required(ErrorMessage = "Vui lòng chọn bác sĩ")]
    public int DoctorId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phòng")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu")]
    public DateTime StartTime { get; set; } = DateTime.Today.AddHours(8);

    public DateTime? EndTime { get; set; }
}
