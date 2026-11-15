using AuraCafé.API.Auth;
using AuraCafé.API.Data;
using AuraCafé.API.Models;
using AuraCafé.API.Services.Bakong;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using kh.gov.nbc.bakong_khqr;

internal sealed class FakeBakongSandboxClient : IBakongSandboxClient
{
    public SandboxTransaction? Result { get; set; }
    public bool Unavailable { get; set; }
    public int Calls { get; private set; }
    public Task<bool> AccountExistsAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    public Task<SandboxTransaction?> FindAsync(string md5, CancellationToken cancellationToken)
    {
        Calls++;
        if (Unavailable) throw new BakongProviderException("Sandbox temporarily unavailable.");
        return Task.FromResult(Result);
    }
}

internal static class BakongSandboxChecks
{
    public static async Task RunAsync(IServiceProvider services, string apiUrl, string adminToken, string cashierToken, Action<bool,string> check)
    {
        var fake = services.GetRequiredService<FakeBakongSandboxClient>();
        using var client = new HttpClient { BaseAddress = new Uri(apiUrl) };
        check((await client.PostAsync("api/payments/bakong-sandbox/orders/1", null)).StatusCode == HttpStatusCode.Unauthorized, "Bakong anonymous start denied");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        check((await client.PostAsync("api/payments/bakong-sandbox/orders/1", null)).StatusCode == HttpStatusCode.Forbidden, "Bakong Admin cannot start cashier checkout");
        var connection = await client.PostAsync("api/payments/bakong-sandbox/connection", null);
        check(connection.IsSuccessStatusCode && !(await connection.Content.ReadAsStringAsync()).Contains("disposable-test-token"), "Bakong connection probe confirms account without exposing token");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);
        check((await client.PostAsync("api/payments/bakong-sandbox/connection", null)).StatusCode == HttpStatusCode.Forbidden, "Bakong Cashier cannot probe server credentials");
        int staffId, shiftId, orderId, secondId, foreignId;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var staff = await db.StaffMembers.SingleAsync(s => s.Email == "Cashier@test.invalid");
            staffId = staff.Id;
            var shift = await db.WorkShifts.SingleOrDefaultAsync(s => s.StaffId == staffId && s.ClockOutUtc == null);
            if (shift is null) { shift = new WorkShift { StaffId = staffId, OpeningCashCents = 5000 }; db.WorkShifts.Add(shift); await db.SaveChangesAsync(); }
            shiftId = shift.Id;
            Order NewOrder(string number, int owner) => new() { OrderNumber = number, SoldByStaffId = owner, WorkShiftId = shiftId, TotalCents = 725 };
            var one = NewOrder("SANDBOX-ONE", staffId); var two = NewOrder("SANDBOX-TWO", staffId);
            var other = new Staff { StaffCode = "SANDBOX-OTHER", FullName = "Other", Email = "sandbox-other@test.invalid" };
            db.StaffMembers.Add(other); await db.SaveChangesAsync();
            var foreign = NewOrder("SANDBOX-FOREIGN", other.Id);
            db.Orders.AddRange(one,two,foreign); await db.SaveChangesAsync();
            orderId = one.Id; secondId = two.Id; foreignId = foreign.Id;
        }
        string PathFor(int id) => $"api/payments/bakong-sandbox/orders/{id}";
        check((await client.PostAsync(PathFor(foreignId), null)).StatusCode == HttpStatusCode.NotFound, "Bakong wrong cashier ownership denied");
        check((await client.PostAsync(PathFor(int.MaxValue), null)).StatusCode == HttpStatusCode.NotFound, "Bakong invalid order denied");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.StaffMembers.Where(s => s.Id == staffId).ExecuteUpdateAsync(s => s.SetProperty(x => x.EmploymentStatus, "Inactive"));
        }
        check((await client.PostAsync(PathFor(orderId),null)).StatusCode == HttpStatusCode.Conflict, "Bakong inactive cashier cannot start QR");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.StaffMembers.Where(s => s.Id == staffId).ExecuteUpdateAsync(s => s.SetProperty(x => x.EmploymentStatus, "Active"));
            await db.WorkShifts.Where(s => s.Id == shiftId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ClockOutUtc, DateTime.UtcNow));
        }
        check((await client.PostAsync(PathFor(orderId),null)).StatusCode == HttpStatusCode.Conflict, "Bakong closed-shift order cannot start QR");
        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkShifts.Where(s => s.Id == shiftId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ClockOutUtc, (DateTime?)null));
        var sandboxOptions = services.GetRequiredService<IOptions<BakongSandboxOptions>>().Value;
        sandboxOptions.Enabled = false;
        check((await client.PostAsync(PathFor(orderId),null)).StatusCode == HttpStatusCode.ServiceUnavailable, "Bakong disabled configuration fails closed");
        sandboxOptions.Enabled = true;
        var startedResponse = await client.PostAsync(PathFor(orderId), null);
        var startedText = await startedResponse.Content.ReadAsStringAsync();
        check(startedResponse.IsSuccessStatusCode, "Bakong sandbox QR starts: " + (startedResponse.IsSuccessStatusCode ? "OK" : startedText));
        var started = JsonSerializer.Deserialize<SandboxIntentDto>(startedText, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        check(started.AmountCents == 725 && started.Currency == "USD" && started.Environment == "Sandbox" && started.QrImageDataUrl!.StartsWith("data:image/png;base64,"), "Bakong QR uses server order amount and local PNG rendering");
        check(!startedText.Contains("disposable-test-token"), "Bakong QR response never exposes provider token");
        var repeats = await Task.WhenAll(Enumerable.Range(0,3).Select(_ => client.PostAsync(PathFor(orderId), null)));
        var identities = await Task.WhenAll(repeats.Select(r => r.Content.ReadFromJsonAsync<SandboxIntentDto>()));
        check(identities.All(i => i!.Id == started.Id), "Bakong simultaneous repeated starts reuse one stored intent");
        var same = await (await client.PostAsync(PathFor(orderId), null)).Content.ReadFromJsonAsync<SandboxIntentDto>();
        check(same!.Id == started.Id, "Bakong repeated start reuses persisted QR identity");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.BakongSandboxIntents.SingleAsync(i => i.OrderId == orderId);
            check(BakongKHQR.Verify(row.QrPayload).Data.Valid, "Bakong SDK validates generated dynamic QR checksum");
            check(row.QrPayload.Contains("010212") && row.QrPayload.Contains("5303840"), "Bakong QR is dynamic and USD");
        }
        async Task ResetThrottle(int id)
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().BakongSandboxIntents.Where(i => i.OrderId == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.LastCheckedAtUtc, (DateTime?)null));
        }
        async Task<HttpResponseMessage> Verify(int id) => await client.PostAsync(PathFor(id) + "/check", null);
        var pending = await (await Verify(orderId)).Content.ReadFromJsonAsync<SandboxIntentDto>();
        check(pending!.Status == "Pending", "Bakong not-found result remains pending");
        var calls = fake.Calls; await Verify(orderId);
        check(fake.Calls == calls, "Bakong repeated polling is throttled on server");
        await ResetThrottle(orderId); fake.Unavailable = true;
        check((await Verify(orderId)).StatusCode == HttpStatusCode.ServiceUnavailable, "Bakong provider failure does not confirm payment");
        fake.Unavailable = false;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var valid = new SandboxTransaction(new string('a',64), "test@devb", "USD", 7.25m, timestamp);
        foreach (var invalid in new[] { valid with { Amount = 7.24m }, valid with { Currency = "KHR" }, valid with { ReceiverAccountId = "wrong@devb" }, valid with { CreatedDateMs = timestamp - 3600000 }, valid with { CreatedDateMs = timestamp + 3600000 }, valid with { Hash = "bad" } })
        {
            await ResetThrottle(orderId); fake.Result = invalid;
            check((await Verify(orderId)).StatusCode == HttpStatusCode.ServiceUnavailable, "Bakong mismatched amount/currency/recipient/time/hash rejected");
        }
        await ResetThrottle(orderId); fake.Result = valid;
        var confirmationResponse = await Verify(orderId);
        var confirmationBody = await confirmationResponse.Content.ReadAsStringAsync();
        check(confirmationResponse.IsSuccessStatusCode, "Bakong verification response: " + confirmationBody);
        var confirmed = JsonSerializer.Deserialize<SandboxIntentDto>(confirmationBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        check(confirmed!.Status == "TestVerified" && confirmed.TransactionHash == valid.Hash, "Bakong matched provider response confirms test intent");
        check((await (await Verify(orderId)).Content.ReadFromJsonAsync<SandboxIntentDto>())!.Id == started.Id, "Bakong repeat verification is idempotent");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            check((await db.Orders.FindAsync(orderId))!.Status == "Pending" && !await db.Payments.AnyAsync(p => p.OrderId == orderId), "Bakong sandbox confirmation cannot alter live payment/order accounting");
        }
        await client.PostAsync(PathFor(secondId), null);
        fake.Result = valid with { CreatedDateMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        check((await Verify(secondId)).StatusCode == HttpStatusCode.Conflict, "Bakong transaction hash cannot confirm another intent");
        await ResetThrottle(secondId); fake.Result = null;
        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().BakongSandboxIntents.Where(i => i.OrderId == secondId).ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAtUtc, DateTime.UtcNow.AddSeconds(-1)));
        var expired = await (await Verify(secondId)).Content.ReadFromJsonAsync<SandboxIntentDto>();
        check(expired!.Status == "Expired" && expired.QrImageDataUrl is null, "Bakong expired QR is hidden without claiming payment failure or retrying transfer");
        check((await (await client.PostAsync(PathFor(secondId), null)).Content.ReadFromJsonAsync<SandboxIntentDto>())!.Id == expired.Id, "Bakong expiry never silently creates a replacement QR");
        using (var scope = services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().BakongSandboxIntents.SingleAsync(i => i.OrderId == secondId);
            timestamp = new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAtUtc,DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().BakongSandboxIntents.Where(i => i.OrderId == secondId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAtUtc, DateTime.UtcNow.AddMilliseconds(-1)));
        }
        // Late reconciliation may confirm a transfer created inside the original QR window.
        await ResetThrottle(secondId);
        fake.Result = valid with { Hash = new string('c',64), CreatedDateMs = timestamp };
        var late = await (await Verify(secondId)).Content.ReadFromJsonAsync<SandboxIntentDto>();
        check(late!.Status == "TestVerified", "Bakong late reconciliation verifies earlier matching payment without generating another QR");
        await ProtocolChecks(check);
    }

    private static async Task ProtocolChecks(Action<bool,string> check)
    {
        var options = Options.Create(new BakongSandboxOptions { Enabled = true, ApiToken = "local-fake-token", ReceiverAccountId = "test@devb" });
        string response = "{\"responseCode\":0,\"errorCode\":null,\"data\":null}";
        using var http = new HttpClient(new StubHandler(async request =>
        {
            check(request.RequestUri!.Host == "sit-api-bakong.nbc.gov.kh" && request.RequestUri.Scheme == "https", "Bakong client uses fixed HTTPS sandbox host");
            check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "local-fake-token", "Bakong client authenticates server-to-server");
            var body = await request.Content!.ReadAsStringAsync();
            check(body.Contains(request.RequestUri.AbsolutePath.Contains("check_bakong_account") ? "accountId" : "md5"), "Bakong client sends official request field");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
        }));
        var provider = new BakongSandboxClient(http, options);
        check(await provider.AccountExistsAsync(default), "Bakong client accepts official account-success envelope");
        response = "{\"responseCode\":1,\"errorCode\":1,\"data\":null}";
        check(await provider.FindAsync(new string('a',32), default) is null, "Bakong client distinguishes transaction not found");
        response = "{\"responseCode\":0,\"data\":{\"hash\":\""+new string('b',64)+"\",\"toAccountId\":\"test@devb\",\"currency\":\"USD\",\"amount\":\"7.25\",\"createdDateMs\":1700000000000}}";
        check((await provider.FindAsync(new string('a',32),default))!.Amount == 7.25m, "Bakong client parses numeric/string provider amounts without floating-point totals");
        response = "{\"responseCode\":0,\"data\":{}}";
        try { await provider.FindAsync(new string('a',32),default); check(false,"Malformed provider response rejected"); }
        catch (BakongProviderException) { check(true,"Bakong malformed successful response cannot confirm payment"); }
    }
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request); }
}
