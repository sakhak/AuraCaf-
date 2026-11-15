using System.ComponentModel.DataAnnotations;
namespace AuraCafé.API.Auth
{
    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";
        [Required]
        public string Password { get; set; } = "";
    }
    public class LoginResponse
    {
        public string AccessToken { get; set; } = "";
        public DateTime ExpiresAtUtc { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
    }
}
