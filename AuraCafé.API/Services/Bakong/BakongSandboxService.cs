using AuraCafé.API.Data;
using AuraCafé.API.Models;
using kh.gov.nbc.bakong_khqr;
using kh.gov.nbc.bakong_khqr.model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using System.Text.RegularExpressions;

namespace AuraCafé.API.Services.Bakong;

public sealed record SandboxIntentDto(Guid Id, int OrderId, int AmountCents, string Currency, string ReceiverAccountId,
    string Status, DateTime ExpiresAtUtc, DateTime? VerifiedAtUtc, string? TransactionHash, string? QrImageDataUrl, string Environment = "Sandbox");

public sealed class BakongSandboxService(AppDbContext db, IBakongSandboxClient provider, IOptions<BakongSandboxOptions> settings)
{
    private async Task<Order> OwnedOrderAsync(int orderId, string userId, bool requireOpenShift)
    {
        var order = await db.Orders.Include(o => o.SoldByStaff).Include(o => o.WorkShift).SingleOrDefaultAsync(o => o.Id == orderId);
        if (order is null || order.SoldByStaff.UserId != userId) throw new KeyNotFoundException();
        if (order.SoldByStaff.EmploymentStatus != "Active" || order.SoldByStaff.Position != "Cashier") throw new InvalidOperationException("An active cashier profile is required.");
        if (requireOpenShift && order.WorkShift.ClockOutUtc is not null) throw new InvalidOperationException("This order's shift must be open to start a sandbox checkout.");
        return order;
    }

    public async Task<SandboxIntentDto?> GetAsync(int orderId, string userId)
    {
        await OwnedOrderAsync(orderId, userId, false);
        var intent = await db.BakongSandboxIntents.AsNoTracking().SingleOrDefaultAsync(i => i.OrderId == orderId);
        return intent is null ? null : ToDto(intent);
    }

    public async Task<SandboxIntentDto> StartAsync(int orderId, string userId, CancellationToken cancellationToken)
    {
        if (!settings.Value.IsConfigured) throw new BakongProviderException("Configure the Bakong sandbox account and API token on the API server first.");
        var order = await OwnedOrderAsync(orderId, userId, true);
        if (order.Status != "Pending" || order.TotalCents <= 0 || await db.Payments.AnyAsync(p => p.OrderId == orderId)) throw new InvalidOperationException("A positive, unpaid pending order is required.");
        var existing = await db.BakongSandboxIntents.SingleOrDefaultAsync(i => i.OrderId == orderId);
        if (existing is not null) return ToDto(existing); // Reuse the persisted QR; never create a second payment request after a timeout.
        if (!await provider.AccountExistsAsync(cancellationToken)) throw new InvalidOperationException("The receiving account does not exist in the Bakong sandbox.");
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        var expires = now.AddMinutes(5);
        var options = settings.Value;
        var generated = BakongKHQR.GenerateIndividual(new IndividualInfo
        {
            BakongAccountID = options.ReceiverAccountId, MerchantName = options.ReceiverName, MerchantCity = options.ReceiverCity,
            Currency = KHQRCurrency.USD, Amount = order.TotalCents / 100d, BillNumber = id.ToString("N")[..24],
            StoreLabel = "Aura Cafe Test", TerminalLabel = "Sandbox", ExpirationTimestamp = new DateTimeOffset(expires).ToUnixTimeMilliseconds()
        });
        if (generated.Status.Code != 0 || string.IsNullOrWhiteSpace(generated.Data?.QR)) throw new InvalidOperationException("The sandbox QR could not be generated. Check merchant configuration.");
        var intent = new BakongSandboxIntent
        {
            Id = id, OrderId = orderId, AmountCents = order.TotalCents, ReceiverAccountId = options.ReceiverAccountId,
            QrPayload = generated.Data.QR, Md5 = generated.Data.MD5.ToLowerInvariant(), CreatedAtUtc = now, ExpiresAtUtc = expires
        };
        db.BakongSandboxIntents.Add(intent);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.Entry(intent).State = EntityState.Detached;
            var concurrent = await db.BakongSandboxIntents.AsNoTracking().SingleOrDefaultAsync(i => i.OrderId == orderId, cancellationToken);
            if (concurrent is null) throw;
            return ToDto(concurrent);
        }
        return ToDto(intent);
    }

    public async Task<SandboxIntentDto> CheckAsync(int orderId, string userId, CancellationToken cancellationToken)
    {
        var order = await OwnedOrderAsync(orderId, userId, false);
        var intent = await db.BakongSandboxIntents.SingleOrDefaultAsync(i => i.OrderId == orderId, cancellationToken) ?? throw new KeyNotFoundException();
        if (intent.Status == "TestVerified") return ToDto(intent);
        if (order.Status != "Pending" || await db.Payments.AnyAsync(p => p.OrderId == orderId, cancellationToken)) throw new InvalidOperationException("This order has already been settled or is no longer pending.");
        var now = DateTime.UtcNow;
        // Atomic server-side throttle covers multiple browser tabs and polling clients.
        var claimed = await db.BakongSandboxIntents.Where(i => i.Id == intent.Id && i.Status != "TestVerified"
            && (i.LastCheckedAtUtc == null || i.LastCheckedAtUtc < now.AddSeconds(-4)))
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.LastCheckedAtUtc, now), cancellationToken);
        if (claimed == 0) return ToDto(intent);
        var result = await provider.FindAsync(intent.Md5, cancellationToken);
        if (result is null)
        {
            if (now >= intent.ExpiresAtUtc)
                await db.BakongSandboxIntents.Where(i => i.Id == intent.Id && i.Status == "Pending").ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, "Expired"), cancellationToken);
        }
        else
        {
            if (!Regex.IsMatch(result.Hash ?? "", @"\A[a-fA-F0-9]{64}\z") || result.ReceiverAccountId != intent.ReceiverAccountId
                || result.Currency != "USD" || result.Amount != intent.AmountCents / 100m
                || result.CreatedDateMs < new DateTimeOffset(DateTime.SpecifyKind(intent.CreatedAtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds() - 1000
                || result.CreatedDateMs > new DateTimeOffset(DateTime.SpecifyKind(intent.ExpiresAtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds())
                throw new BakongProviderException("The transaction does not match this order's receiver, USD amount or checkout time. Nothing was confirmed.");
            var hash = result.Hash!.ToLowerInvariant();
            // The unique transaction-hash index prevents one transfer confirming multiple sandbox orders.
            try
            {
                await db.BakongSandboxIntents.Where(i => i.Id == intent.Id && i.Status != "TestVerified")
                    .ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, "TestVerified").SetProperty(i => i.TransactionHash, hash)
                        .SetProperty(i => i.VerifiedAtUtc, now), cancellationToken);
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
            { throw new InvalidOperationException("That transaction is already linked to another sandbox checkout."); }
        }
        await db.Entry(intent).ReloadAsync(cancellationToken);
        return ToDto(intent);
    }

    private static SandboxIntentDto ToDto(BakongSandboxIntent intent)
    {
        string? image = null;
        var state = intent.Status == "Pending" && intent.ExpiresAtUtc <= DateTime.UtcNow ? "Expired" : intent.Status;
        if (state == "Pending")
        {
            using var generator = new QRCodeGenerator();
            using var code = generator.CreateQrCode(intent.QrPayload, QRCodeGenerator.ECCLevel.M);
            using var png = new PngByteQRCode(code);
            image = "data:image/png;base64," + Convert.ToBase64String(png.GetGraphic(8));
        }
        return new(intent.Id, intent.OrderId, intent.AmountCents, "USD", intent.ReceiverAccountId, state,
            intent.ExpiresAtUtc, intent.VerifiedAtUtc, intent.TransactionHash, image);
    }
}
