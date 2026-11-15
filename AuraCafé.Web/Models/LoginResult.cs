namespace AuraCafé.Web.Models;

public class LoginResult
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
}
