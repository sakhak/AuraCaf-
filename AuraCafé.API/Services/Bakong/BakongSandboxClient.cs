using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AuraCafé.API.Services.Bakong;

public sealed class BakongSandboxClient(HttpClient http, IOptions<BakongSandboxOptions> settings) : IBakongSandboxClient
{
    // Fixed SIT host: a test token can never silently switch this integration to production.
    private static readonly Uri BaseUri = new("https://sit-api-bakong.nbc.gov.kh/");
    private BakongSandboxOptions Options => settings.Value;

    private async Task<JsonDocument> PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        if (!Options.IsConfigured) throw new BakongProviderException("Configure the Bakong sandbox account and API token on the API server first.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.ApiToken);
        request.Content = JsonContent.Create(body);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new BakongProviderException(response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    ? "Bakong rejected the test credentials. Check the sandbox token on the API server."
                    : "Bakong sandbox is unavailable. Check again later; no payment has been recorded.");
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { throw new BakongProviderException("The Bakong sandbox result could not be confirmed. Check again later; no payment has been recorded."); }
    }

    public async Task<bool> AccountExistsAsync(CancellationToken cancellationToken)
    {
        using var response = await PostAsync("v1/check_bakong_account", new { accountId = Options.ReceiverAccountId }, cancellationToken);
        var root = response.RootElement;
        if (root.TryGetProperty("responseCode", out var code) && code.TryGetInt32(out var result))
        {
            if (result == 0) return true;
            if (result == 1 && root.TryGetProperty("errorCode", out var error) && error.TryGetInt32(out var number) && number == 11) return false;
        }
        throw new BakongProviderException("Bakong did not confirm the test receiving account.");
    }

    public async Task<SandboxTransaction?> FindAsync(string md5, CancellationToken cancellationToken)
    {
        using var response = await PostAsync("v1/check_transaction_by_md5", new { md5 }, cancellationToken);
        var root = response.RootElement;
        if (!root.TryGetProperty("responseCode", out var code) || !code.TryGetInt32(out var result))
            throw new BakongProviderException("Bakong returned an invalid transaction response.");
        if (result != 0)
        {
            if (result == 1 && root.TryGetProperty("errorCode", out var error) && error.TryGetInt32(out var number) && number == 1) return null;
            throw new BakongProviderException("Bakong did not confirm a successful test transaction.");
        }
        try
        {
            var data = root.GetProperty("data");
            var amountText = data.GetProperty("amount").ToString();
            var timestampText = data.GetProperty("createdDateMs").ToString();
            var amount = decimal.Parse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            var timestamp = decimal.Parse(timestampText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            if (timestamp != decimal.Truncate(timestamp)) throw new FormatException();
            return new SandboxTransaction(data.GetProperty("hash").GetString()!, data.GetProperty("toAccountId").GetString()!,
                data.GetProperty("currency").GetString()!, amount, checked((long)timestamp));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new BakongProviderException("Bakong returned incomplete transaction details. Nothing was confirmed."); }
    }
}
