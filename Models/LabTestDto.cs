using System.ComponentModel.DataAnnotations;

namespace SmartClinic.Models;

public class LabTestDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên xét nghiệm không được để trống")]
    [StringLength(255, MinimumLength = 3,
          ErrorMessage = "Tên xét nghiệm phải từ 3-255 ký tự")]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    [Range(1, 10000000, ErrorMessage = "Giá phải lớn hơn 0")]

    public decimal Price { get; set; }
    public string? Unit { get; set; }
    public int? DefaultRoomId { get; set; }
    public Room? DefaultRoom { get; set; }
}
