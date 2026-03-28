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

/// <summary>
/// DTO cho yêu cầu phân lịch tự động.
/// Admin chọn khoảng ngày, danh sách bác sĩ, phòng, ca muốn phân.
/// </summary>
public class AutoScheduleRequestDto
{
    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
    public DateTime FromDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
    public DateTime ToDate { get; set; } = DateTime.Today.AddDays(6);

    /// <summary>Danh sách ID bác sĩ tham gia phân lịch</summary>
    public List<int> SelectedDoctorIds { get; set; } = new();

    /// <summary>Danh sách ID phòng khám cần phân</summary>
    public List<int> SelectedRoomIds { get; set; } = new();

    /// <summary>Danh sách ID ca trực cần phân</summary>
    public List<int> SelectedShiftDefinitionIds { get; set; } = new();

    /// <summary>Có ghi đè lên ca đã có sẵn không? Mặc định: không (chỉ điền ô trống)</summary>
    public bool OverwriteExisting { get; set; } = false;
}

/// <summary>
/// Một ô trong kết quả preview phân lịch tự động.
/// </summary>
public class AutoSchedulePreviewItemDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int ShiftDefinitionId { get; set; }
    public string ShiftName { get; set; } = string.Empty;

    /// <summary>true nếu ô này ghi đè ca cũ (chỉ khi OverwriteExisting = true)</summary>
    public bool IsOverwrite { get; set; }
}

/// <summary>
/// Kết quả sau khi xác nhận lưu phân lịch tự động.
/// </summary>
public class AutoScheduleResultDto
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public int TotalCreated { get; set; }
    public int TotalSkipped { get; set; }
    public string Summary { get; set; } = string.Empty;
}
