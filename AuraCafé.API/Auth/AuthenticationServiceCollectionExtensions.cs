using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using AuraCafé.API.Data;
using System.Security.Claims;
using System.Text;

namespace AuraCafé.API.Auth;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddSignInManager()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Missing Jwt:Key.");

        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 bytes.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = configuration["Jwt:Audience"],
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = ClaimTypes.Name,
                        RoleClaimType = ClaimTypes.Role
                    };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userId = context.Principal?
                            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                        var users = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<ApplicationUser>>();

                        var user = userId is null
                            ? null
                            : await users.FindByIdAsync(userId);

                        var stamp = context.Principal?
                            .FindFirst("security_stamp")?.Value;

                        if (user is null ||
                            !user.IsActive ||
                            stamp != await users.GetSecurityStampAsync(user))
                        {
                            context.Fail("Account is no longer valid.");
                            return;
                        }

                        var currentRoles = await users.GetRolesAsync(user);
                        var tokenRoles = context.Principal!
                            .FindAll(ClaimTypes.Role)
                            .Select(claim => claim.Value)
                            .OrderBy(role => role)
                            .ToArray();

                        if (!currentRoles.OrderBy(role => role)
                            .SequenceEqual(tokenRoles))
                        {
                            context.Fail("Roles have changed.");
                        }
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        services.AddScoped<JwtTokenService>();

        return services;
    }
}
