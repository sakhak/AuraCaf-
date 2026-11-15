using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace AuraCafé.API.Auth
{
    public class JwtTokenService
    {
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _users;

        public JwtTokenService(IConfiguration config, UserManager<ApplicationUser> users)
        {
            _config = config;
            _users = users;
        }
        public async Task<LoginResponse> CreateAsync(ApplicationUser user)
        {
            var roles = await _users.GetRolesAsync(user);

            var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT signing key is missing.");

            var issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT issuer is missing.");

            var audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT audience is missing.");

            var expires = DateTime.UtcNow.AddMinutes(15);

            var claims = new List<Claim> 
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.FullName),
                new("security_stamp", await _users.GetSecurityStampAsync(user))

            };
            foreach(var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
               issuer: issuer,
               audience: audience,
               claims: claims,
               expires: expires,
               signingCredentials: credentials);

            return new LoginResponse
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAtUtc = expires,
                FullName = user.FullName,
                Role = roles.FirstOrDefault() ?? ""
            };

        }
    }
}