
using System.ComponentModel.DataAnnotations;

namespace AuraCafé.API.DTOs.Staff;

public class CreateStaffDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [StringLength(30)]
    public string PhoneNumber { get; set; } = "";

    [Required]
    public string Position { get; set; } = "Cashier";

    [Required]
    public DateOnly HireDate { get; set; }

    // Required only when creating a Cashier login.
    public string? Password { get; set; }
}
