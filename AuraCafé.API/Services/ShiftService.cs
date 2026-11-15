
using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Shifts;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Interfaces.Shifts;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Services;

public class ShiftService : IShiftService
{
    private readonly IShiftRepository _repository;
    private readonly AppDbContext _context;

    public ShiftService(
        IShiftRepository repository,
        AppDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    private async Task<Staff> GetStaffAsync(string userId)
    {
        var staff = await _context.StaffMembers
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (staff is null)
            throw new InvalidOperationException(
                "No employee profile is linked to this account.");

        return staff;
    }

    public async Task<ShiftResponseDto?> GetCurrentAsync(
        string userId)
    {
        var staff = await GetStaffAsync(userId);

        var shift = await _repository.GetOpenShiftAsync(
            staff.Id);

        return shift is null ? null : ToResponse(shift);
    }

    public async Task<List<ShiftResponseDto>>
        GetMyHistoryAsync(string userId)
    {
        var staff = await GetStaffAsync(userId);

        var shifts = await _repository.GetByStaffAsync(
            staff.Id);

        return shifts.Select(ToResponse).ToList();
    }

    public async Task<ShiftResponseDto> ClockInAsync(
        string userId, int? openingCashCents = null, long? openingCashRiel = null)
    {
        var staff = await GetStaffAsync(userId);

        if (staff.EmploymentStatus != "Active" ||
            staff.Position != "Cashier")
        {
            throw new InvalidOperationException(
                "This employee cannot start a cashier shift.");
        }

        if (openingCashCents < 0 || openingCashRiel < 0) throw new ArgumentException("Opening cash cannot be negative.");

        var existing = await _repository.GetOpenShiftAsync(
            staff.Id);

        if (existing is not null)
            throw new InvalidOperationException(
                "You already have an open shift.");

        var shift = new WorkShift
        {
            StaffId = staff.Id,
            Staff = staff,
            ClockInUtc = DateTime.UtcNow,
            OpeningCashCents = openingCashCents,
            OpeningCashRiel = openingCashRiel,
            Status = "Open"
        };

        await _repository.AddAsync(shift);

        try
        {
            await _repository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The unique index also protects against
            // two simultaneous clock-in requests.
            throw new InvalidOperationException(
                "Unable to start shift. You may already " +
                "have an open shift.");
        }

        return ToResponse(shift);
    }

    public async Task<ShiftResponseDto> ClockOutAsync(
        string userId)
    {
        var staff = await GetStaffAsync(userId);

        var shift = await _repository.GetOpenShiftAsync(
            staff.Id);

        if (shift is null)
            throw new InvalidOperationException(
                "You do not have an open shift.");

        if (shift.OpeningCashCents.HasValue)
            throw new InvalidOperationException("Submit the full drawer count through shift reconciliation before closing this shift.");

        var clockOutUtc = DateTime.UtcNow;

        // Update only if the shift is still open.
        // This prevents two simultaneous requests
        // from successfully closing the same shift.
        var updated = await _context.WorkShifts
            .Where(x =>
                x.Id == shift.Id &&
                x.ClockOutUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ClockOutUtc,
                    (DateTime?)clockOutUtc)
                .SetProperty(x => x.Status, "Closed"));

        if (updated != 1)
            throw new InvalidOperationException(
                "This shift has already been closed.");

        shift.ClockOutUtc = clockOutUtc;
        shift.Status = "Closed";

        return ToResponse(shift);
    }

    public async Task<List<ShiftResponseDto>> GetAllAsync()
    {
        var shifts = await _repository.GetAllAsync();

        return shifts.Select(ToResponse).ToList();
    }

    private static ShiftResponseDto ToResponse(
        WorkShift shift)
    {
        decimal? hours = null;

        if (shift.ClockOutUtc.HasValue)
        {
            var duration =
                shift.ClockOutUtc.Value - shift.ClockInUtc;

            hours = Math.Round(
                (decimal)duration.TotalHours,
                2,
                MidpointRounding.AwayFromZero);
        }

        return new ShiftResponseDto
        {
            Id = shift.Id,
            OpeningCashCents = shift.OpeningCashCents,
            OpeningCashRiel = shift.OpeningCashRiel,
            StaffId = shift.StaffId,
            StaffCode = shift.Staff.StaffCode,
            StaffName = shift.Staff.FullName,
            ClockInUtc = shift.ClockInUtc,
            ClockOutUtc = shift.ClockOutUtc,
            Status = shift.Status,
            HoursWorked = hours
        };
    }
}
