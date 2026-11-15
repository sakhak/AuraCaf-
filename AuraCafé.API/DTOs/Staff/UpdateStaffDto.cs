
using System.ComponentModel.DataAnnotations;

namespace AuraCafé.API.DTOs.Staff;

public class UpdateStaffDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [StringLength(30)]
    public string PhoneNumber { get; set; } = "";

    [Required]
    public string Position { get; set; } = "Cashier";

    [Required]
    public DateOnly HireDate { get; set; }
}
