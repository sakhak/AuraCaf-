
namespace AuraCafé.API.DTOs.Staff;

public class StaffResponseDto
{
    public int Id { get; set; }
    public string StaffCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Position { get; set; } = "";
    public DateOnly HireDate { get; set; }
    public string EmploymentStatus { get; set; } = "";
    public bool HasLoginAccount { get; set; }
    public DateTime CreatedAt { get; set; }
}
