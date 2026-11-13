namespace AuraCafé.Web.Models;

public class CafeInfo
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string BaseCurrency { get; set; } = "";
    public List<string> SupportedCurrencies { get; set; } = new();
    public string Status { get; set; } = "";
}