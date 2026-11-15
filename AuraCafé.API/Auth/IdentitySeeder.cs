
using AuraCafé.API.Data;
using AuraCafé.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Auth;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration config)
    {
        var roles = services.GetRequiredService<
            RoleManager<IdentityRole>>();

        var users = services.GetRequiredService<
            UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();

        foreach (var role in new[] { "Admin", "Cashier" })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(
                    new IdentityRole(role));

                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        "Could not create role.");
            }
        }

        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Development admin credentials are missing.");

        var admin = await users.FindByEmailAsync(email);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "Aura Café Administrator"
            };

            var created = await users.CreateAsync(
                admin, password);

            if (!created.Succeeded)
                throw new InvalidOperationException(
                    string.Join("; ",
                        created.Errors.Select(e => e.Description)));

            var assigned = await users.AddToRoleAsync(
                admin, "Admin");

            if (!assigned.Succeeded)
                throw new InvalidOperationException(
                    "Could not assign Admin role.");
        }

        if (!await users.IsInRoleAsync(admin, "Admin"))
        {
            var assigned = await users.AddToRoleAsync(
                admin, "Admin");

            if (!assigned.Succeeded)
                throw new InvalidOperationException(
                    "Could not assign Admin role.");
        }



        var cashierEmail = config["SeedCashier:Email"];
        var cashierPassword = config["SeedCashier:Password"];

        if (!string.IsNullOrWhiteSpace(cashierEmail) &&
            !string.IsNullOrWhiteSpace(cashierPassword))
        {
            var cashier = await users.FindByEmailAsync(cashierEmail);

            if (cashier is null)
            {
                cashier = new ApplicationUser
                {
                    UserName = cashierEmail,
                    Email = cashierEmail,
                    EmailConfirmed = true,
                    FullName = "Test Cashier"
                };

                var created = await users.CreateAsync(
                    cashier, cashierPassword);

                if (!created.Succeeded)
                    throw new InvalidOperationException(
                        string.Join("; ",
                            created.Errors.Select(e => e.Description)));
            }

            if (!await users.IsInRoleAsync(cashier, "Cashier"))
            {
                var assigned = await users.AddToRoleAsync(
                    cashier, "Cashier");

                if (!assigned.Succeeded)
                    throw new InvalidOperationException(
                        "Could not assign Cashier role.");
            }

            var staff = await db.StaffMembers
                .SingleOrDefaultAsync(x => x.UserId == cashier.Id);

            if (staff is null)
            {
                staff = await db.StaffMembers
                    .SingleOrDefaultAsync(x =>
                        x.Email == cashier.Email);

                if (staff is not null)
                {
                    if (staff.UserId is not null ||
                        staff.Position != "Cashier")
                    {
                        throw new InvalidOperationException(
                            "The seeded cashier email is already " +
                            "assigned to another staff profile.");
                    }

                    staff.UserId = cashier.Id;
                    await db.SaveChangesAsync();
                }
                else
                {
                    staff = new Staff
                    {
                        StaffCode = "TMP-" +
                            Guid.NewGuid().ToString("N"),
                        FullName = cashier.FullName,
                        Email = cashier.Email!,
                        Position = "Cashier",
                        HireDate = DateOnly.FromDateTime(
                            DateTime.UtcNow),
                        EmploymentStatus = "Active",
                        UserId = cashier.Id
                    };

                    await db.StaffMembers.AddAsync(staff);
                    await db.SaveChangesAsync();

                    staff.StaffCode = $"EMP-{staff.Id:D4}";
                    await db.SaveChangesAsync();
                }
            }
        }

    }
}
