namespace AuraCafé.Web.Models;
public class BakongSandboxIntentDto
{
    public Guid Id { get; set; }
    public int OrderId { get; set; }
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "USD";
    public string ReceiverAccountId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? TransactionHash { get; set; }
    public string? QrImageDataUrl { get; set; }
    public string Environment { get; set; } = "Sandbox";
}
public class BakongSandboxConnectionDto
{
    public bool AccountExists { get; set; }
    public string ReceiverAccountId { get; set; } = "";
}
