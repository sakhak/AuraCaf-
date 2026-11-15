using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AuraCafé.API.Auth;
namespace AuraCafé.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly JwtTokenService _tokens;

        public AuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        JwtTokenService tokens)
        {
            _users = users;
            _signIn = signIn;
            _tokens = tokens;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request)
        {
            var user = await _users.FindByEmailAsync(
                request.Email.Trim());

            if (user is null || !user.IsActive)
                return Unauthorized("Invalid credentials.");

            var result = await _signIn.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true);

            if (!result.Succeeded)
                return Unauthorized("Invalid credentials.");

            return Ok(await _tokens.CreateAsync(user));
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new
            {
                UserId = User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier
                )?.Value,
                Name = User.Identity?.Name,
                Roles = User.FindAll(
                    System.Security.Claims.ClaimTypes.Role
                ).Select(x => x.Value)
            });
        }
    }
}
