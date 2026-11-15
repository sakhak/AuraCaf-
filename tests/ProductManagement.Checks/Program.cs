using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuraCafé.API.Auth;
using AuraCafé.API.Controllers;
using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Products;
using AuraCafé.API.Extensions;
using AuraCafé.API.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

// Real HTTP/JWT/EF Core/SQLite regression checks on a unique disposable database.
var database = Path.Combine(Path.GetTempPath(), $"aura-products-{Guid.NewGuid():N}.db");
var builder = WebApplication.CreateBuilder();
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:DefaultConnection"] = $"Data Source={database}",
    ["Jwt:Key"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
    ["Jwt:Issuer"] = "ProductRegression", ["Jwt:Audience"] = "ProductRegression"
});
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["BakongSandbox:Enabled"] = "true", ["BakongSandbox:ApiToken"] = "disposable-test-token",
    ["BakongSandbox:ReceiverAccountId"] = "test@devb"
});
builder.Services.AddSingleton<FakeBakongSandboxClient>();
builder.Services.AddSingleton<AuraCafé.API.Services.Bakong.IBakongSandboxClient>(sp => sp.GetRequiredService<FakeBakongSandboxClient>());
builder.Services.AddControllers().AddApplicationPart(typeof(ProductsController).Assembly);
await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
var passed = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
    passed++;
}
try
{
    await CurrencyMigrationChecks.RunAsync(Check);
    string adminToken, cashierToken;
    int historicalId, categoryId;
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var registration in builder.Services.Where(d =>
            d.ServiceType.Assembly == typeof(ProductsController).Assembly &&
            d.ServiceType.IsInterface && d.Lifetime == ServiceLifetime.Scoped))
        {
            Check(scope.ServiceProvider.GetRequiredService(registration.ServiceType) is not null,
                "API DI resolves " + registration.ServiceType.Name);
        }
        foreach (var controller in typeof(ProductsController).Assembly.GetTypes().Where(t =>
            !t.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t)))
        {
            Check(ActivatorUtilities.CreateInstance(scope.ServiceProvider, controller) is not null,
                "API controller activates: " + controller.Name);
        }
        // Start with the previous schema and a historical product, then apply the additive migration.
        await db.GetService<IMigrator>().MigrateAsync("20261114142645_AddPaymentManagement");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Categories (Name) VALUES ('Coffee')");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Products (Name,Description,PriceCents,IsAvailable,CategoryId,CreatedAt) VALUES ('Historic Coffee','Original',800,1,1,'2026-11-01 00:00:00')");
        await db.Database.MigrateAsync();
        var historical = await db.Products.SingleAsync();
        historicalId = historical.Id;
        categoryId = historical.CategoryId;
        Check(historical.IsActive && historical.ImageUrl == "" && historical.PriceCents == 800, "Migration preserves existing products with active default");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Cashier" }) await roles.CreateAsync(new IdentityRole(role));
        async Task<ApplicationUser> AddUser(string role)
        {
            var user = new ApplicationUser { UserName = role + "@test.invalid", Email = role + "@test.invalid", FullName = role, IsActive = true };
            var result = await users.CreateAsync(user, "TestOnly!Password123");
            if (!result.Succeeded) throw new Exception(string.Join(",", result.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, role);
            return user;
        }
        var admin = await AddUser("Admin");
        var cashier = await AddUser("Cashier");
        var tokens = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        adminToken = (await tokens.CreateAsync(admin)).AccessToken;
        cashierToken = (await tokens.CreateAsync(cashier)).AccessToken;
        var staff = new Staff { StaffCode = "TEST-1", FullName = "Cashier", Email = cashier.Email!, UserId = cashier.Id, HireDate = new DateOnly(2026, 1, 1) };
        db.StaffMembers.Add(staff);
        var shift = new WorkShift { Staff = staff, ClockInUtc = DateTime.UtcNow };
        db.WorkShifts.Add(shift);
        db.Orders.Add(new Order
        {
            OrderNumber = "TEST-HISTORY", SoldByStaff = staff, WorkShift = shift, TotalCents = 800,
            Items = [new OrderItem { ProductId = historicalId, ProductName = historical.Name, UnitPriceCents = 800, Quantity = 1, SubtotalCents = 800 }]
        });
        await db.SaveChangesAsync();
    }
    await app.StartAsync();
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    var valid = new { Name = "New Tea", Description = "Fresh", PriceCents = 275, CategoryId = categoryId, IsAvailable = true, IsActive = true, ImageUrl = "https://example.com/tea.jpg" };
    Check((await client.GetAsync("api/products/manage")).StatusCode == HttpStatusCode.Unauthorized, "Anonymous management denied");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);
    Check((await client.PostAsJsonAsync("api/products", valid)).StatusCode == HttpStatusCode.Forbidden, "Cashier cannot create products");
    Check((await client.PutAsJsonAsync($"api/products/{historicalId}", valid)).StatusCode == HttpStatusCode.Forbidden, "Cashier cannot edit products");
    Check((await client.DeleteAsync($"api/products/{historicalId}")).StatusCode == HttpStatusCode.Forbidden, "Cashier cannot deactivate products");
    Check((await client.GetAsync("api/products/manage")).StatusCode == HttpStatusCode.Forbidden, "Cashier cannot access management search");
    Check((await client.GetAsync("api/products")).IsSuccessStatusCode, "Cashier retains catalog read access");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
    Check((await client.GetAsync("api/auth/me")).StatusCode == HttpStatusCode.OK, "Valid Identity-issued JWT reaches authenticated endpoint");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
    Check((await client.GetAsync("api/auth/me")).StatusCode == HttpStatusCode.Unauthorized, "Malformed JWT is rejected");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
    var createdResponse = await client.PostAsJsonAsync("api/products", valid);
    Check(createdResponse.StatusCode == HttpStatusCode.Created, "Admin creates product");
    var created = (await createdResponse.Content.ReadFromJsonAsync<ProductResponseDto>())!;
    Check(created.PriceCents == 275 && created.ImageUrl == valid.ImageUrl, "Image and integer-cent price persisted");
    Check((await client.PostAsJsonAsync("api/products", valid)).StatusCode == HttpStatusCode.BadRequest, "Duplicate name/category rejected");
    foreach (var invalid in new object[]
    {
        new { Name = "Zero", PriceCents = 0, CategoryId = categoryId },
        new { Name = " a ", PriceCents = 100, CategoryId = categoryId },
        new { Name = "Missing category", PriceCents = 100, CategoryId = int.MaxValue },
        new { Name = "Unsafe", PriceCents = 100, CategoryId = categoryId, ImageUrl = "javascript:alert(1)" },
        new { Name = "Unsafe credentials", PriceCents = 100, CategoryId = categoryId, ImageUrl = "https://user:secret@example.com/a.jpg" },
        new { Name = "Decimal cents", PriceCents = 1.5, CategoryId = categoryId },
        new { Name = "Overflow", PriceCents = 2147483648L, CategoryId = categoryId }
    })
        Check((await client.PostAsJsonAsync("api/products", invalid)).StatusCode == HttpStatusCode.BadRequest, "Invalid product rejected: " + invalid);
    Check((await client.GetAsync("api/products/manage?page=0")).StatusCode == HttpStatusCode.BadRequest, "Invalid page rejected");
    Check((await client.GetAsync("api/products/manage?pageSize=101")).StatusCode == HttpStatusCode.BadRequest, "Page size bounded");
    var page = await client.GetFromJsonAsync<ProductPageDto>("api/products/manage?pageSize=1&page=2");
    Check(page!.TotalCount == 2 && page.Items.Count == 1, "SQLite pagination returns total and requested slice");
    var filtered = await client.GetFromJsonAsync<ProductPageDto>($"api/products/manage?search=TEA&categoryId={categoryId}&active=true&available=true");
    Check(filtered!.TotalCount == 1 && filtered.Items.Single().Id == created.Id, "Combined case-insensitive search/category/status filters");
    var empty = await client.GetFromJsonAsync<ProductPageDto>("api/products/manage?search=no-such-product");
    Check(empty!.TotalCount == 0 && empty.Items.Count == 0, "No matches returns empty page");
    var update = new { Name = "Updated Tea", PriceCents = 325, CategoryId = categoryId, IsAvailable = false, IsActive = false, ImageUrl = "" };
    Check((await client.PutAsJsonAsync($"api/products/{created.Id}", update)).StatusCode == HttpStatusCode.NoContent, "Admin edits availability/status/price");
    var updated = await client.GetFromJsonAsync<ProductResponseDto>($"api/products/{created.Id}");
    Check(!updated!.IsActive && !updated.IsAvailable && updated.PriceCents == 325, "Edited values persisted");
    Check((await client.PutAsJsonAsync($"api/products/{created.Id}", new { update.Name, update.PriceCents, update.CategoryId, IsAvailable = true, IsActive = true })).IsSuccessStatusCode, "Admin reactivates product");
    Check((await client.PutAsJsonAsync($"api/products/{created.Id}", new { Name = "Historic Coffee", PriceCents = 325, CategoryId = categoryId })).StatusCode == HttpStatusCode.BadRequest, "Edit cannot duplicate another product");
    var inactiveResponse = await client.PostAsJsonAsync("api/products", new { Name = "Retired", PriceCents = 100, CategoryId = categoryId, IsActive = false, IsAvailable = false });
    var inactive = await inactiveResponse.Content.ReadFromJsonAsync<ProductResponseDto>();
    Check(inactiveResponse.StatusCode == HttpStatusCode.Created && !inactive!.IsActive && !inactive.IsAvailable, "Create respects inactive and unavailable values");
    var inactivePage = await client.GetFromJsonAsync<ProductPageDto>("api/products/manage?active=false&available=false");
    Check(inactivePage!.Items.Any(p => p.Id == inactive!.Id), "Inactive/unavailable filters return matching products");
    Check((await client.DeleteAsync($"api/products/{historicalId}")).IsSuccessStatusCode, "Referenced product safely deactivated");
    Check((await client.DeleteAsync($"api/products/{historicalId}")).IsSuccessStatusCode, "Repeated deactivation is safe");
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.OrderItems.SingleAsync();
        Check(item.ProductId == historicalId && item.UnitPriceCents == 800 && item.ProductName == "Historic Coffee", "Historical order snapshot and product reference preserved");
        Check(await db.Products.AnyAsync(p => p.Id == historicalId && !p.IsActive), "Historical product row retained");
    }
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);
    Check((await client.PostAsJsonAsync("api/orders", new { Items = new[] { new { ProductId = historicalId, Quantity = 1 } } })).StatusCode == HttpStatusCode.BadRequest, "Server rejects inactive product in new order");
    await CategoryChecks.RunAsync(app.Urls.Single(), adminToken, cashierToken, Check);
    var inventoryBalances = await InventoryChecks.RunAsync(app.Services, app.Urls.Single(), adminToken, cashierToken, created.Id, Check);
    await Lesson12Checks.RunAsync(app.Services, app.Urls.Single(), adminToken, cashierToken, created.Id, Check);
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var afterSales = await db.Ingredients.ToDictionaryAsync(i => i.Id, i => i.QuantityOnHand);
        Check(inventoryBalances.All(pair => afterSales[pair.Key] == pair.Value), "Inventory sales/payments do not automatically deduct stock in Lesson13");
    }
    string originalCashierName;
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exportCashier = await db.StaffMembers.SingleAsync(s => s.Email == "Cashier@test.invalid");
        originalCashierName = exportCashier.FullName;
        exportCashier.FullName = "=1+1,\"Cafe\"\nTeam"; // Disposable fixture for spreadsheet escaping; never touches working data.
        await db.SaveChangesAsync();
    }
    try { await WebChecks.RunAsync(app.Urls.Single(), categoryId, historicalId, Check); }
    finally
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exportCashier = await db.StaffMembers.SingleAsync(s => s.Email == "Cashier@test.invalid");
        exportCashier.FullName = originalCashierName;
        await db.SaveChangesAsync();
    }
    await BakongSandboxChecks.RunAsync(app.Services, app.Urls.Single(), adminToken, cashierToken, Check);
    Console.WriteLine($"Completed: {passed} checks passed. Working database was not used.");
}
finally
{
    await app.StopAsync();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    foreach (var suffix in new[] { "", "-shm", "-wal" })
        if (File.Exists(database + suffix)) File.Delete(database + suffix);
}
