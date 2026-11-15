
using System.ComponentModel.DataAnnotations;

namespace AuraCafé.Web.Models;

public class StaffDto
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

public class CreateStaffForm
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(30)]
    public string PhoneNumber { get; set; } = "";

    [Required]
    public string Position { get; set; } = "Cashier";

    [Required]
    public DateOnly? HireDate { get; set; } =
        ReportDisplay.Today;

    [DataType(DataType.Password)]
    public string? Password { get; set; }
}

public class EditStaffForm
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(30)]
    public string PhoneNumber { get; set; } = "";

    [Required]
    public string Position { get; set; } = "Cashier";

    [Required]
    public DateOnly? HireDate { get; set; }
}
