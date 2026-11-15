using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Shifts;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Services;

public class ShiftReconciliationService(AppDbContext db) : IShiftReconciliationService
{
    public async Task<ShiftCashDto?> GetAsync(int shiftId, string userId, bool isAdmin)
    {
        var shift = await db.WorkShifts.AsNoTracking().Include(s => s.Staff).SingleOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null || (!isAdmin && shift.Staff.UserId != userId)) return null;
        var saved = await db.ShiftReconciliations.AsNoTracking().SingleOrDefaultAsync(r => r.WorkShiftId == shiftId);
        if (saved is not null)
            return new ShiftCashDto
            {
                WorkShiftId = shift.Id, StaffId = shift.StaffId, CashierName = shift.Staff.FullName, ShiftStatus = shift.Status,
                OpeningCashRiel = saved.OpeningCashRiel, ExpectedCashRiel = saved.ExpectedCashRiel, CountedCashRiel = saved.CountedCashRiel, DifferenceCashRiel = saved.DifferenceCashRiel,
                OpeningCashCents = saved.OpeningCashCents, RevenueCents = saved.RevenueCents, PaidOrders = saved.PaidOrders,
                CashReceivedCents = saved.CashReceivedCents, ChangeCents = saved.ChangeCents, ExpectedCashCents = saved.ExpectedCashCents,
                CountedCashCents = saved.CountedCashCents, DifferenceCents = saved.DifferenceCents, SubmittedAtUtc = saved.SubmittedAtUtc, Note = saved.Note
            };
        return await CalculateAsync(shift);
    }

    private async Task<ShiftCashDto> CalculateAsync(WorkShift shift)
    {
        if (shift.OpeningCashCents < 0 || shift.OpeningCashRiel < 0) throw new InvalidOperationException("Recorded opening float is invalid.");
        var payments = db.Payments.Where(p => p.WorkShiftId == shift.Id);
        if (await payments.AnyAsync(p => p.Method != "Cash" || p.Status != "Completed" || p.Order.Status != "Paid"
            || p.AmountCents < 0 || p.AmountCents != p.Order.TotalCents || p.CashReceivedCents < p.AmountCents
            || (long)p.ChangeCents != (long)p.CashReceivedCents - p.AmountCents || p.CollectedByStaffId != shift.StaffId
            || p.CollectedByStaffId != p.Order.SoldByStaffId
            || p.Order.Items.Any(i => i.Quantity <= 0 || i.UnitPriceCents < 0 || (long)i.SubtotalCents != (long)i.UnitPriceCents * i.Quantity)
            || p.Order.Items.Sum(i => (long)i.SubtotalCents) != p.AmountCents))
            throw new InvalidOperationException("Payment integrity check failed for this shift.");
        var totals = await payments.GroupBy(_ => 1).Select(g => new ShiftCashDto
        {
            RevenueCents = g.Sum(p => (long)p.AmountCents), PaidOrders = g.Count(),
            CashReceivedCents = g.Sum(p => (long)p.CashReceivedCents), ChangeCents = g.Sum(p => (long)p.ChangeCents)
        }).SingleOrDefaultAsync() ?? new ShiftCashDto();
        totals.WorkShiftId = shift.Id;
        totals.StaffId = shift.StaffId;
        totals.CashierName = shift.Staff.FullName;
        totals.ShiftStatus = shift.Status;
        totals.OpeningCashCents = shift.OpeningCashCents;
        totals.OpeningCashRiel = shift.OpeningCashRiel;
        totals.ExpectedCashRiel = shift.OpeningCashRiel;
        totals.ExpectedCashCents = shift.OpeningCashCents.HasValue ? checked(shift.OpeningCashCents.Value + totals.RevenueCents) : null;
        return totals;
    }

    public async Task<ShiftCashDto> CloseAsync(int shiftId, string userId, CloseShiftCashDto request)
    {
        if (request.CountedCashCents < 0 || request.CountedCashRiel < 0 || request.Note?.Length > 500) throw new ArgumentException("Invalid counted cash or note.");
        // Uses the same immediate SQLite writer transaction as cash collection: either the payment
        // commits before this snapshot or subsequent cash collection sees the shift closed.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var shift = await db.WorkShifts.Include(s => s.Staff).SingleOrDefaultAsync(s => s.Id == shiftId);
        if (shift is null || shift.Staff.UserId != userId) throw new KeyNotFoundException();
        if (shift.Staff.EmploymentStatus != "Active" || shift.Staff.Position != "Cashier") throw new InvalidOperationException("An active cashier profile is required.");
        if (shift.ClockOutUtc is not null || shift.Status != "Open" || await db.ShiftReconciliations.AnyAsync(r => r.WorkShiftId == shiftId))
            throw new InvalidOperationException("This shift is already closed or reconciled. Refresh its reconciliation before any follow-up.");
        if (!shift.OpeningCashCents.HasValue) throw new InvalidOperationException("Opening float was not recorded for this legacy shift. Full-drawer reconciliation is unavailable.");
        if (shift.OpeningCashRiel.HasValue && !request.CountedCashRiel.HasValue)
            throw new ArgumentException("Enter the full KHR drawer count, including zero when empty.");
        if (!shift.OpeningCashRiel.HasValue && request.CountedCashRiel.HasValue)
            throw new ArgumentException("This legacy shift has no KHR opening record. Do not invent a KHR difference.");
        var cash = await CalculateAsync(shift);
        var now = DateTime.UtcNow;
        var record = new ShiftReconciliation
        {
            WorkShiftId = shift.Id, SubmittedByStaffId = shift.StaffId, OpeningCashCents = shift.OpeningCashCents.Value,
            OpeningCashRiel = shift.OpeningCashRiel, ExpectedCashRiel = shift.OpeningCashRiel, CountedCashRiel = request.CountedCashRiel,
            DifferenceCashRiel = request.CountedCashRiel.HasValue ? checked(request.CountedCashRiel.Value - shift.OpeningCashRiel!.Value) : null,
            RevenueCents = cash.RevenueCents, PaidOrders = cash.PaidOrders, CashReceivedCents = cash.CashReceivedCents, ChangeCents = cash.ChangeCents,
            ExpectedCashCents = cash.ExpectedCashCents!.Value, CountedCashCents = request.CountedCashCents,
            DifferenceCents = checked(request.CountedCashCents - cash.ExpectedCashCents.Value), SubmittedAtUtc = now, Note = request.Note?.Trim() ?? ""
        };
        db.ShiftReconciliations.Add(record);
        shift.ClockOutUtc = now;
        shift.Status = "Closed";
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (await GetAsync(shiftId, userId, false))!;
    }
}
