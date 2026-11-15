namespace AuraCafé.API.Services.Bakong;

public class BakongSandboxOptions
{
    public bool Enabled { get; set; }
    public string ApiToken { get; set; } = "";
    public string ReceiverAccountId { get; set; } = "";
    public string ReceiverName { get; set; } = "Sakhak Loeung";
    public string ReceiverCity { get; set; } = "PHNOM PENH";
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiToken)
        && System.Text.RegularExpressions.Regex.IsMatch(ReceiverAccountId, @"\A[a-zA-Z0-9_.-]+@[a-zA-Z0-9_.-]+\z")
        && ReceiverAccountId.Length <= 32 && ReceiverName.Length is > 0 and <= 25 && ReceiverCity.Length is > 0 and <= 15;
}

public sealed class BakongProviderException(string message) : Exception(message);
public sealed record SandboxTransaction(string Hash, string ReceiverAccountId, string Currency, decimal Amount, long CreatedDateMs);
public interface IBakongSandboxClient
{
    Task<bool> AccountExistsAsync(CancellationToken cancellationToken);
    Task<SandboxTransaction?> FindAsync(string md5, CancellationToken cancellationToken);
}
