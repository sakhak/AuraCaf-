
using AuraCafé.API.Auth;

namespace AuraCafé.API.Models;

public class Staff
{
    public int Id { get; set; }

    public string StaffCode { get; set; } = "";

    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string PhoneNumber { get; set; } = "";

    public string Position { get; set; } = "Cashier";

    public DateOnly HireDate { get; set; }

    public string EmploymentStatus { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }
}
