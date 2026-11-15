using AuraCafé.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class CurrencyMigrationChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var path = Path.Combine(Path.GetTempPath(), "aura-currency-upgrade-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + path).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261114162235_AddInventoryManagement");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO Staff (StaffCode,FullName,Email,PhoneNumber,Position,HireDate,EmploymentStatus,CreatedAt,UserId) VALUES ('LEGACY','Legacy Cashier','legacy@test.invalid','','Cashier','2026-01-01','Active','2026-11-01',NULL)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO WorkShifts (StaffId,ClockInUtc,ClockOutUtc,Status,CreatedAtUtc,OpeningCashCents) VALUES (1,'2026-11-01','2026-11-02','Closed','2026-11-01',5000)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO ShiftReconciliations (WorkShiftId,SubmittedByStaffId,OpeningCashCents,RevenueCents,PaidOrders,CashReceivedCents,ChangeCents,ExpectedCashCents,CountedCashCents,DifferenceCents,SubmittedAtUtc,Note) VALUES (1,1,5000,0,0,0,0,5000,4900,-100,'2026-11-02','Legacy count')");
            await db.Database.MigrateAsync();
            var shift = await db.WorkShifts.SingleAsync();
            var reconciliation = await db.ShiftReconciliations.SingleAsync();
            check(shift.OpeningCashCents == 5000 && shift.OpeningCashRiel is null, "Currency migration preserves old USD opening and unknown KHR");
            check(reconciliation.CountedCashCents == 4900 && reconciliation.DifferenceCents == -100 && reconciliation.OpeningCashRiel is null
                && reconciliation.ExpectedCashRiel is null && reconciliation.CountedCashRiel is null && reconciliation.DifferenceCashRiel is null,
                "Currency migration preserves immutable historical USD reconciliation without inventing KHR");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-shm", "-wal" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }
}
