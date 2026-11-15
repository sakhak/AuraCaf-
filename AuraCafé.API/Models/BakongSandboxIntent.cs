namespace AuraCafé.API.Models;

// Sandbox confirmations never enter Payments or change an order to Paid.
public class BakongSandboxIntent
{
    public Guid Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int AmountCents { get; set; }
    public string ReceiverAccountId { get; set; } = "";
    public string QrPayload { get; set; } = "";
    public string Md5 { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? TransactionHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastCheckedAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
}
