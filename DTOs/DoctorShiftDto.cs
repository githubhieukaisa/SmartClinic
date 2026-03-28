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
    public int Capacity { get; set; }
    public string ShiftName { get; set; } = string.Empty;
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

    [Required(ErrorMessage = "Vui lòng chọn ngày trực")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Vui lòng chọn ca trực")]
    public int ShiftDefinitionId { get; set; }
}

public class DoctorShiftWeeklyUpdateDto
{
    public int Id { get; set; } // 0 if new, otherwise existing shift ID
    public int DoctorId { get; set; }
    public int RoomId { get; set; }
    public DateTime Date { get; set; }
    public int ShiftDefinitionId { get; set; }
    public bool IsDeleted { get; set; }
}
