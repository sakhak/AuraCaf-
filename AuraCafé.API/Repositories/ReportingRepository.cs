using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Reports;
using AuraCafé.API.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Repositories;

public class ReportingRepository(AppDbContext db) : IReportingRepository
{
    public async Task<SalesReportDto> ReadAsync(DateOnly from, DateOnly to, DateTime startUtc, DateTime endUtc)
    {
        // A deferred SQLite read transaction keeps aggregates in one snapshot without taking a writer lock.
        await db.Database.OpenConnectionAsync();
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var enlisted = await db.Database.UseTransactionAsync(transaction);
        var period = db.Payments.AsNoTracking().Where(p => p.PaidAtUtc >= startUtc && p.PaidAtUtc < endUtc);
        if (await period.AnyAsync(p => p.Status != "Completed" || p.Method != "Cash" || p.Order.Status != "Paid"
            || p.AmountCents < 0 || p.AmountCents != p.Order.TotalCents || p.CashReceivedCents < p.AmountCents
            || (long)p.ChangeCents != (long)p.CashReceivedCents - p.AmountCents
            || p.WorkShift.StaffId != p.CollectedByStaffId
            || p.CollectedByStaffId != p.Order.SoldByStaffId
            || p.Order.Items.Any(i => i.Quantity <= 0 || i.UnitPriceCents < 0 || (long)i.SubtotalCents != (long)i.UnitPriceCents * i.Quantity)
            || p.Order.Items.Sum(i => (long)i.SubtotalCents) != p.AmountCents)
            || await db.Orders.AnyAsync(o => o.Status == "Paid" && o.CreatedAtUtc >= startUtc && o.CreatedAtUtc < endUtc
                && !db.Payments.Any(p => p.OrderId == o.Id && p.Status == "Completed")))
            throw new InvalidOperationException("Payment integrity check failed. Reports are unavailable until the affected records are reviewed.");

        var report = await period.GroupBy(_ => 1).Select(g => new SalesReportDto
        {
            RevenueCents = g.Sum(p => (long)p.AmountCents), PaidOrders = g.Count(),
            CashReceivedCents = g.Sum(p => (long)p.CashReceivedCents), ChangeCents = g.Sum(p => (long)p.ChangeCents)
        }).SingleOrDefaultAsync() ?? new SalesReportDto();
        report.From = from;
        report.To = to;
        report.GeneratedAtUtc = DateTime.UtcNow;
        report.PendingOrders = await db.Orders.CountAsync(o => o.Status == "Pending");
        report.OrdersCreated = await db.Orders.CountAsync(o => o.CreatedAtUtc >= startUtc && o.CreatedAtUtc < endUtc);
        // Asia/Bangkok is UTC+07:00; group in SQLite, rather than loading every payment into memory.
        report.Daily = await period.GroupBy(p => p.PaidAtUtc.AddHours(7).Date).Select(g => new SalesPeriodDto
        {
            Date = g.Key, RevenueCents = g.Sum(p => (long)p.AmountCents), PaidOrders = g.Count()
        }).OrderBy(x => x.Date).ToListAsync();
        report.Monthly = report.Daily.GroupBy(x => new { x.Date.Year, x.Date.Month }).Select(g => new SalesPeriodDto
        {
            Date = new DateTime(g.Key.Year, g.Key.Month, 1), RevenueCents = g.Sum(x => x.RevenueCents), PaidOrders = g.Sum(x => x.PaidOrders)
        }).ToList();
        report.Cashiers = await period.GroupBy(p => new { p.CollectedByStaffId, p.CollectedByStaff.FullName }).Select(g => new CashierSalesDto
        {
            StaffId = g.Key.CollectedByStaffId, Name = g.Key.FullName, PaidOrders = g.Count(),
            RevenueCents = g.Sum(p => (long)p.AmountCents), CashReceivedCents = g.Sum(p => (long)p.CashReceivedCents), ChangeCents = g.Sum(p => (long)p.ChangeCents)
        }).OrderByDescending(x => x.RevenueCents).ThenBy(x => x.StaffId).ToListAsync();
        report.Products = await db.OrderItems.AsNoTracking().Where(i => period.Any(p => p.OrderId == i.OrderId))
            .GroupBy(i => i.ProductId).Select(g => new ProductSalesDto
            {
                ProductId = g.Key, Name = g.Min(i => i.ProductName) ?? "", Quantity = g.Sum(i => (long)i.Quantity), RevenueCents = g.Sum(i => (long)i.SubtotalCents)
            }).OrderByDescending(x => x.Quantity).ThenBy(x => x.ProductId).Take(20).ToListAsync();
        await transaction.CommitAsync();
        return report;
    }
}
