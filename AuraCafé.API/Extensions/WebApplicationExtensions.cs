using AuraCafé.API.Auth;
using AuraCafé.API.Data;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Extensions;

public static class WebApplicationExtensions
{
    public static async Task ConfigureApiPipelineAsync(
        this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();

            await IdentitySeeder.SeedAsync(
                scope.ServiceProvider,
                app.Configuration);
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }
}
