using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuraCafé.API.Auth;
using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Orders;
using AuraCafé.API.DTOs.Payments;
using AuraCafé.API.DTOs.Reports;
using AuraCafé.API.DTOs.Shifts;
using AuraCafé.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class Lesson12Checks
{
    public static async Task RunAsync(IServiceProvider services, string url, string adminToken, string cashierToken, int productId, Action<bool, string> check)
    {
        using var client = new HttpClient { BaseAddress = new Uri(url) };
        const string reportPath = "api/reports/sales?from=2026-11-14&to=2026-11-14";
        check((await client.GetAsync(reportPath)).StatusCode == HttpStatusCode.Unauthorized, "Lesson12 anonymous report denied");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);
        check((await client.GetAsync(reportPath)).StatusCode == HttpStatusCode.Forbidden, "Lesson12 Cashier cannot read Admin reports");
        var oldShift = (await client.GetFromJsonAsync<ShiftResponseDto>("api/shifts/my/current"))!;
        var legacy = await client.GetFromJsonAsync<ShiftCashDto>($"api/shifts/{oldShift.Id}/reconciliation");
        check(legacy!.OpeningCashCents is null && legacy.ExpectedCashCents is null, "Lesson12 migration preserves unknown opening cash for legacy shifts");
        check((await client.PostAsJsonAsync($"api/shifts/{oldShift.Id}/reconciliation", new { CountedCashCents = 0 })).StatusCode == HttpStatusCode.Conflict, "Lesson12 legacy shift cannot fabricate drawer difference");
        await client.PostAsync("api/shifts/clock-out", null);
        check((await client.PostAsJsonAsync("api/shifts/clock-in", new { OpeningCashCents = -1 })).StatusCode == HttpStatusCode.BadRequest, "Lesson12 negative opening float rejected");
        var opened = await client.PostAsJsonAsync("api/shifts/clock-in", new { OpeningCashCents = 1000 });
        var shift = (await opened.Content.ReadFromJsonAsync<ShiftResponseDto>())!;
        check(opened.IsSuccessStatusCode && shift.OpeningCashCents == 1000, "Lesson12 opening float captured at clock-in");
        check((await client.PostAsync("api/shifts/clock-out", null)).StatusCode == HttpStatusCode.Conflict, "Lesson12 recorded-float shifts cannot bypass cash-count closure");
        var orderResponse = await client.PostAsJsonAsync("api/orders", new { Items = new[] { new { ProductId = productId, Quantity = 1 } } });
        var order = (await orderResponse.Content.ReadFromJsonAsync<OrderResponseDto>())!;
        var paymentResponse = await client.PostAsJsonAsync("api/payments/cash", new { OrderId = order.Id, CashReceivedCents = 500 });
        var payment = (await paymentResponse.Content.ReadFromJsonAsync<PaymentResponseDto>())!;
        check(paymentResponse.StatusCode == HttpStatusCode.Created && payment.AmountCents == 325 && payment.ChangeCents == 175, "Lesson12 reuses existing server-priced cash workflow");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Payments.SingleAsync(p => p.Id == payment.Id);
            saved.PaidAtUtc = new DateTime(2026, 11, 13, 17, 0, 0, DateTimeKind.Utc);
            var originalOrder = await db.Orders.SingleAsync(o => o.Id == order.Id);
            originalOrder.CreatedAtUtc = saved.PaidAtUtc.AddDays(-1);
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        check((await client.GetAsync("api/reports/sales?from=2026-11-15&to=2026-11-14")).StatusCode == HttpStatusCode.BadRequest, "Lesson12 reversed date range rejected");
        check((await client.GetAsync("api/reports/sales?from=2020-01-01&to=2026-11-14")).StatusCode == HttpStatusCode.BadRequest, "Lesson12 date range bounded");
        var report = await client.GetFromJsonAsync<SalesReportDto>(reportPath);
        check(report!.RevenueCents == 325 && report.CashReceivedCents == 500 && report.ChangeCents == 175 && report.PaidOrders == 1, "Lesson12 revenue excludes tender/change and pending orders");
        check(report.Daily.Single().Date == new DateTime(2026, 11, 14), "Lesson12 local midnight lower boundary included");
        check(report.Cashiers.Single().StaffId == payment.CollectedByStaffId && report.Products.Single().RevenueCents == 325, "Lesson12 cashier/product breakdowns reconcile");
        var empty = await client.GetFromJsonAsync<SalesReportDto>("api/reports/sales?from=2025-01-01&to=2025-01-01");
        check(empty!.RevenueCents == 0 && empty.PaidOrders == 0 && empty.Daily.Count == 0, "Lesson12 verified empty period has zero totals");
        // Seed synthetic completed sales in the disposable database, including >200 rows,
        // two-line orders, the exclusive date boundary and a 64-bit revenue sum.
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 202; i++)
            {
                var amount = i == 0 ? int.MaxValue : 1;
                var sale = new Order { OrderNumber = "REPORT-" + i, SoldByStaffId = shift.StaffId, WorkShiftId = oldShift.Id, Status = "Paid", TotalCents = amount,
                    Items = [new OrderItem { ProductId = productId, ProductName = "Historical Tea", Quantity = 1, UnitPriceCents = amount, SubtotalCents = amount }] };
                db.Payments.Add(new Payment { Order = sale, CollectedByStaffId = shift.StaffId, WorkShiftId = shift.Id, AmountCents = amount, CashReceivedCents = amount, PaidAtUtc = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc) });
            }
            var otherId = await db.Products.Where(p => p.Id != productId).Select(p => p.Id).FirstAsync();
            var twoLines = new Order { OrderNumber = "TWO-LINES", SoldByStaffId = shift.StaffId, WorkShiftId = shift.Id, Status = "Paid", TotalCents = 3,
                Items = [new OrderItem { ProductId = productId, ProductName = "Historical Tea", Quantity = 1, UnitPriceCents = 1, SubtotalCents = 1 }, new OrderItem { ProductId = otherId, ProductName = "Historical Coffee", Quantity = 1, UnitPriceCents = 2, SubtotalCents = 2 }] };
            db.Payments.Add(new Payment { Order = twoLines, CollectedByStaffId = shift.StaffId, WorkShiftId = shift.Id, AmountCents = 3, CashReceivedCents = 3, PaidAtUtc = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc) });
            var boundaryOrder = new Order { OrderNumber = "NEXT-DAY", SoldByStaffId = shift.StaffId, WorkShiftId = shift.Id, Status = "Paid", TotalCents = 400,
                Items = [new OrderItem { ProductId = productId, ProductName = "Historical Tea", Quantity = 1, UnitPriceCents = 400, SubtotalCents = 400 }] };
            db.Payments.Add(new Payment { Order = boundaryOrder, CollectedByStaffId = shift.StaffId, WorkShiftId = shift.Id, AmountCents = 400, CashReceivedCents = 400, PaidAtUtc = new DateTime(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc) });
            await db.SaveChangesAsync();
        }
        const long expectedRevenue = (long)int.MaxValue + 201 + 325 + 3;
        report = await client.GetFromJsonAsync<SalesReportDto>(reportPath);
        check(report!.RevenueCents == expectedRevenue && report.PaidOrders == 204, "Lesson12 all >200 payments and 64-bit sums included");
        check(report.Products.Sum(p => p.RevenueCents) == expectedRevenue && report.Cashiers.Sum(p => p.RevenueCents) == expectedRevenue, "Lesson12 multiple lines do not double-count parent payments");
        var nextDay = await client.GetFromJsonAsync<SalesReportDto>("api/reports/sales?from=2026-11-15&to=2026-11-15");
        check(nextDay!.RevenueCents == 400 && nextDay.PaidOrders == 1, "Lesson12 exclusive midnight belongs to next local date");
        var month = await client.GetFromJsonAsync<SalesReportDto>("api/reports/sales?from=2026-11-01&to=2026-11-30");
        check(month!.Monthly.Single().RevenueCents == expectedRevenue + 400, "Lesson12 monthly aggregate matches daily sums");
        check((await client.PostAsJsonAsync($"api/shifts/{shift.Id}/reconciliation", new { CountedCashCents = 0 })).StatusCode == HttpStatusCode.Forbidden, "Lesson12 Admin cannot submit cashier cash count");
        string otherToken;
        using (var scope = services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var other = new ApplicationUser { UserName = "other@test.invalid", Email = "other@test.invalid", FullName = "Other Cashier" };
            await users.CreateAsync(other, "TestOnly!Password123");
            await users.AddToRoleAsync(other, "Cashier");
            otherToken = (await scope.ServiceProvider.GetRequiredService<JwtTokenService>().CreateAsync(other)).AccessToken;
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        check((await client.GetAsync($"api/shifts/{shift.Id}/reconciliation")).StatusCode == HttpStatusCode.NotFound, "Lesson12 other cashier cannot read shift reconciliation");
        check((await client.PostAsJsonAsync($"api/shifts/{shift.Id}/reconciliation", new { CountedCashCents = 0 })).StatusCode == HttpStatusCode.NotFound, "Lesson12 other cashier cannot close shift");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);
        check((await client.PostAsJsonAsync($"api/shifts/{shift.Id}/reconciliation", new { CountedCashCents = -1 })).StatusCode == HttpStatusCode.BadRequest, "Lesson12 negative drawer count rejected");
        var cash = await client.GetFromJsonAsync<ShiftCashDto>($"api/shifts/{shift.Id}/reconciliation");
        check(cash!.OpeningCashRiel is null && cash.ExpectedCashRiel is null, "Legacy USD-only shift does not acquire invented KHR opening balance");
        check((await client.PostAsJsonAsync($"api/shifts/{shift.Id}/reconciliation", new { CountedCashCents = 0, CountedCashRiel = 100 })).StatusCode == HttpStatusCode.BadRequest,
            "Legacy shift cannot submit a KHR difference without recorded opening riel");
        var expectedDrawer = expectedRevenue + 400 + 1000;
        check(cash!.ExpectedCashCents == expectedDrawer && cash.RevenueCents == expectedRevenue + 400, "Lesson12 full-shift cash uses collection shift across dates plus opening float");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailCount BEFORE INSERT ON ShiftReconciliations BEGIN SELECT RAISE(ABORT, 'Injected count failure'); END;");
        }
        check((await client.PostAsJsonAsync($"api/shifts/{shift.Id}/reconciliation", new { CountedCashCents = expectedDrawer })).StatusCode == HttpStatusCode.Conflict, "Lesson12 failed count insert returns controlled conflict");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            check((await db.WorkShifts.FindAsync(shift.Id))!.ClockOutUtc is null && !await db.ShiftReconciliations.AnyAsync(r => r.WorkShiftId == shift.Id), "Lesson12 failed count insert rolls back shift closure");
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailCount;");
        }
        var closures = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync($"api/shifts/{shift.Id}/reconciliation", new { CountedCashCents = expectedDrawer - 30, Note = "Test shortage" })));
        check(closures.Count(r => r.IsSuccessStatusCode) == 1 && closures.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Lesson12 simultaneous closes commit exactly once");
        cash = await client.GetFromJsonAsync<ShiftCashDto>($"api/shifts/{shift.Id}/reconciliation");
        check(cash!.ShiftStatus == "Closed" && cash.SubmittedAtUtc.HasValue && cash.DifferenceCents == -30 && cash.Note == "Test shortage", "Lesson12 GET reconciles saved close result and signed shortage");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            check(await db.ShiftReconciliations.CountAsync(r => r.WorkShiftId == shift.Id) == 1 && (await db.WorkShifts.FindAsync(shift.Id))!.ClockOutUtc.HasValue, "Lesson12 count and closure persist atomically");
            var saved = await db.Payments.SingleAsync(p => p.Id == payment.Id);
            saved.ChangeCents++;
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        check((await client.GetAsync(reportPath)).StatusCode == HttpStatusCode.Conflict, "Lesson12 corrupt payment causes explicit report integrity error");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Payments.SingleAsync(p => p.Id == payment.Id);
            saved.ChangeCents--;
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);
        var racingShiftResponse = await client.PostAsJsonAsync("api/shifts/clock-in", new { OpeningCashCents = 0 });
        var racingShift = (await racingShiftResponse.Content.ReadFromJsonAsync<ShiftResponseDto>())!;
        int pendingId;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            pendingId = await db.Orders.Where(o => o.OrderNumber == "TEST-HISTORY").Select(o => o.Id).SingleAsync();
        }
        var collectTask = client.PostAsJsonAsync("api/payments/cash", new { OrderId = pendingId, CashReceivedCents = 1000 });
        var closeTask = client.PostAsJsonAsync($"api/shifts/{racingShift.Id}/reconciliation", new { CountedCashCents = 0 });
        await Task.WhenAll(collectTask, closeTask);
        check(closeTask.Result.IsSuccessStatusCode && (collectTask.Result.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict), "Lesson12 concurrent payment/close produces controlled outcomes");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var collected = await db.Payments.SingleOrDefaultAsync(p => p.OrderId == pendingId);
            var reconciliation = await db.ShiftReconciliations.SingleAsync(r => r.WorkShiftId == racingShift.Id);
            check(reconciliation.RevenueCents == (collected?.AmountCents ?? 0) && reconciliation.ExpectedCashCents == reconciliation.RevenueCents,
                "Lesson12 payment is either included in closure snapshot or rejected");
            if (collected is not null)
            {
                // Keep the fixed November 14 report fixture unchanged for Web assertions.
                collected.PaidAtUtc = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);
                await db.SaveChangesAsync();
            }
        }
        check((await client.PostAsJsonAsync("api/shifts/clock-in", new { OpeningCashCents = 0, OpeningCashRiel = -1 })).StatusCode == HttpStatusCode.BadRequest, "Negative opening KHR rejected");
        check((await client.PostAsJsonAsync("api/shifts/clock-in", new { OpeningCashCents = 0, OpeningCashRiel = 1.5 })).StatusCode == HttpStatusCode.BadRequest, "Fractional opening riel rejected");
        var rielOpened = await client.PostAsJsonAsync("api/shifts/clock-in", new { OpeningCashCents = 1000, OpeningCashRiel = 200000 });
        var rielShift = (await rielOpened.Content.ReadFromJsonAsync<ShiftResponseDto>())!;
        check(rielShift.OpeningCashCents == 1000 && rielShift.OpeningCashRiel == 200000, "USD cents and whole KHR persist in separate fields");
        var rielCash = await client.GetFromJsonAsync<ShiftCashDto>($"api/shifts/{rielShift.Id}/reconciliation");
        check(rielCash!.ExpectedCashCents == 1000 && rielCash.ExpectedCashRiel == 200000 && rielCash.RevenueCents == 0, "KHR float does not inflate USD revenue or expected USD cash");
        check((await client.PostAsJsonAsync($"api/shifts/{rielShift.Id}/reconciliation", new { CountedCashCents = 1000 })).StatusCode == HttpStatusCode.BadRequest, "Recorded KHR float requires explicit KHR closing count");
        check((await client.PostAsJsonAsync($"api/shifts/{rielShift.Id}/reconciliation", new { CountedCashCents = 1000, CountedCashRiel = 1.5 })).StatusCode == HttpStatusCode.BadRequest, "Fractional closing riel rejected");
        var rielClosed = await client.PostAsJsonAsync($"api/shifts/{rielShift.Id}/reconciliation", new { CountedCashCents = 1000, CountedCashRiel = 195000 });
        check(rielClosed.IsSuccessStatusCode, "Dual-currency shift closes with explicit independent counts");
        rielCash = await client.GetFromJsonAsync<ShiftCashDto>($"api/shifts/{rielShift.Id}/reconciliation");
        check(rielCash!.DifferenceCents == 0 && rielCash.DifferenceCashRiel == -5000, "KHR shortage leaves USD reconciliation unchanged");
        await client.PostAsJsonAsync("api/shifts/clock-in", new { OpeningCashCents = 0 });
    }
}
