namespace SmartClinic.Models;

public class LabTestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Unit { get; set; }
    public int? DefaultRoomId { get; set; }
    public Room? DefaultRoom { get; set; }
}
