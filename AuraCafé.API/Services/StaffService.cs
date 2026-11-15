
using AuraCafé.API.Auth;
using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Staff;
using AuraCafé.API.Interfaces.StaffManagement;
using AuraCafé.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Services;

public class StaffService : IStaffService
{
    private readonly IStaffRepository _repository;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _users;

    public StaffService(
        IStaffRepository repository,
        AppDbContext context,
        UserManager<ApplicationUser> users)
    {
        _repository = repository;
        _context = context;
        _users = users;
    }

    public async Task<List<StaffResponseDto>> GetAllAsync()
    {
        var staff = await _repository.GetAllAsync();

        return staff.Select(ToResponse).ToList();
    }

    public async Task<StaffResponseDto?> GetByIdAsync(int id)
    {
        var staff = await _repository.GetByIdAsync(id);

        return staff is null ? null : ToResponse(staff);
    }

    public async Task<StaffResponseDto> CreateAsync(
        CreateStaffDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var fullName = dto.FullName.Trim();
        var position = dto.Position.Trim();

        if (position is not ("Cashier" or "Barista"))
            throw new ArgumentException(
                "Position must be Cashier or Barista.");

        if (dto.HireDate == default ||
            dto.HireDate > DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(1)))
            throw new ArgumentException(
                "Invalid hire date.");

        if (await _repository.EmailExistsAsync(email) ||
            await _users.FindByEmailAsync(email) is not null)
            throw new ArgumentException(
                "Email already exists.");

        if (position == "Cashier" &&
            string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException(
                "Cashier password is required.");

        if (position != "Cashier" &&
            !string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException(
                "Only Cashiers receive login accounts.");

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        ApplicationUser? user = null;

        if (position == "Cashier")
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true
            };

            var created = await _users.CreateAsync(
                user, dto.Password!);

            if (!created.Succeeded)
                throw new ArgumentException(
                    string.Join("; ",
                        created.Errors.Select(e => e.Description)));

            var assigned = await _users.AddToRoleAsync(
                user, "Cashier");

            if (!assigned.Succeeded)
                throw new InvalidOperationException(
                    "Could not assign Cashier role.");
        }

        var staff = new Staff
        {
            StaffCode = "TMP-" + Guid.NewGuid().ToString("N"),
            FullName = fullName,
            Email = email,
            PhoneNumber = dto.PhoneNumber.Trim(),
            Position = position,
            HireDate = dto.HireDate,
            EmploymentStatus = "Active",
            UserId = user?.Id
        };

        await _repository.AddAsync(staff);
        await _repository.SaveChangesAsync();

        staff.StaffCode = $"EMP-{staff.Id:D4}";

        await _repository.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToResponse(staff);
    }

    public async Task<bool> UpdateAsync(
        int id, UpdateStaffDto dto)
    {
        var staff = await _repository.GetByIdAsync(id);

        if (staff is null)
            return false;

        var position = dto.Position.Trim();

        if (position is not ("Cashier" or "Barista"))
            throw new ArgumentException(
                "Position must be Cashier or Barista.");

        if (dto.HireDate == default ||
            dto.HireDate > DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(1)))
            throw new ArgumentException(
                "Invalid hire date.");

        // Position changes involving login access need a
        // dedicated workflow. Do not change roles silently.
        if (position != staff.Position &&
            (position == "Cashier" || staff.Position == "Cashier"))
            throw new ArgumentException(
                "Changing to or from Cashier requires " +
                "a separate account-management workflow.");

        staff.FullName = dto.FullName.Trim();
        staff.PhoneNumber = dto.PhoneNumber.Trim();
        staff.Position = position;
        staff.HireDate = dto.HireDate;

        if (staff.UserId is not null)
        {
            var user = await _users.FindByIdAsync(staff.UserId);

            if (user is not null)
            {
                user.FullName = staff.FullName;

                var result = await _users.UpdateAsync(user);

                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        "Could not update login profile.");
            }
        }

        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetStatusAsync(
        int id, bool active)
    {
        var staff = await _repository.GetByIdAsync(id);

        if (staff is null)
            return false;

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        staff.EmploymentStatus =
            active ? "Active" : "Inactive";

        if (staff.UserId is not null)
        {
            var user = await _users.FindByIdAsync(staff.UserId);

            if (user is not null)
            {
                user.IsActive = active;

                var result = await _users.UpdateAsync(user);

                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        "Could not update account status.");

                // Invalidate existing JWTs and sessions
                // that validate the Identity security stamp.
                var stampResult =
                    await _users.UpdateSecurityStampAsync(user);

                if (!stampResult.Succeeded)
                    throw new InvalidOperationException(
                        "Could not invalidate login sessions.");
            }
        }

        await _repository.SaveChangesAsync();
        await transaction.CommitAsync();

        return true;
    }

    private static StaffResponseDto ToResponse(Staff staff)
    {
        return new StaffResponseDto
        {
            Id = staff.Id,
            StaffCode = staff.StaffCode,
            FullName = staff.FullName,
            Email = staff.Email,
            PhoneNumber = staff.PhoneNumber,
            Position = staff.Position,
            HireDate = staff.HireDate,
            EmploymentStatus = staff.EmploymentStatus,
            HasLoginAccount = staff.UserId is not null,
            CreatedAt = staff.CreatedAt
        };
    }
}
