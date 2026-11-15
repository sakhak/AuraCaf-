
using System.Security.Claims;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Optional explicit key directory for hosts without a writable user profile.
// Existing deployments keep their default key storage when this is not configured.
var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
if (!string.IsNullOrWhiteSpace(keyDirectory))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
}



builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Admin"));

    options.AddPolicy("CashierOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Cashier"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaFolder(
        "Admin", "/", "AdminOnly");

    options.Conventions.AuthorizeAreaFolder(
        "Cashier", "/", "CashierOnly");

    options.Conventions.AuthorizeFolder(
        "/Products", "AdminOnly");

    options.Conventions.AuthorizeFolder(
        "/Categories", "AdminOnly");

    options.Conventions.AddPageRoute("/Categories/Index", "/Admin/Categories");
    options.Conventions.AddPageRoute("/Products/Index", "/Admin/Products");
    options.Conventions.AddPageRoute("/Products/Create", "/Admin/Products/Create");
    options.Conventions.AddPageRoute("/Products/Edit", "/Admin/Products/Edit/{id:int}");

    options.Conventions.AllowAnonymousToPage(
        "/Account/Login");
});


builder.Services.AddMemoryCache();
builder.Services.AddSingleton<AuthSessionStore>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ApiTokenHandler>();


builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AuraCafe.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";

        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        options.SlidingExpiration = false;

        options.Events.OnValidatePrincipal = context =>
        {
            var id = context.Principal?
                .FindFirst("session_id")?.Value;

            var store = context.HttpContext
                .RequestServices.GetRequiredService<
                    AuthSessionStore>();

            if (id is null || store.Get(id) is null)
            {
                context.RejectPrincipal();

                return context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);
            }

            return Task.CompletedTask;
        };
    });

builder.Services.AddHttpClient("AuraCaféAPI", client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException(
            "API BaseUrl is not configured.");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler<ApiTokenHandler>();



builder.Services.AddTransient<CafeApiService>(sp =>
    new CafeApiService(
        sp.GetRequiredService<IHttpClientFactory>()
          .CreateClient("AuraCaféAPI")));

builder.Services.AddTransient<ProductApiService>(sp =>
    new ProductApiService(
        sp.GetRequiredService<IHttpClientFactory>()
          .CreateClient("AuraCaféAPI")));

builder.Services.AddTransient<AuthApiService>(sp =>
    new AuthApiService(
        sp.GetRequiredService<IHttpClientFactory>()
          .CreateClient("AuraCaféAPI")));

builder.Services.AddTransient<StaffApiService>(sp =>
    new StaffApiService(
        sp.GetRequiredService<IHttpClientFactory>()
          .CreateClient("AuraCaféAPI")));

builder.Services.AddTransient<ShiftApiService>(sp =>
    new ShiftApiService(
        sp.GetRequiredService<IHttpClientFactory>()
          .CreateClient("AuraCaféAPI")));

builder.Services.AddTransient<PosProductApiService>(sp =>
    new PosProductApiService(
        sp.GetRequiredService<IHttpClientFactory>()
          .CreateClient("AuraCaféAPI")));

builder.Services.AddTransient<OrderApiService>(sp =>
    new OrderApiService(
        sp.GetRequiredService<IHttpClientFactory>()
          .CreateClient("AuraCaféAPI")));

builder.Services.AddTransient<PaymentApiService>(sp =>
    new PaymentApiService(
        sp.GetRequiredService<IHttpClientFactory>()
          .CreateClient("AuraCaféAPI")));




builder.Services.AddTransient<ReportingApiService>(sp =>
    new ReportingApiService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("AuraCaféAPI")));

builder.Services.AddTransient<InventoryApiService>(sp =>
    new InventoryApiService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("AuraCaféAPI")));

builder.Services.AddTransient<BakongSandboxApiService>(sp => new BakongSandboxApiService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("AuraCaféAPI")));
builder.Services.AddSingleton<ProductImageStore>();
builder.Services.AddSingleton<MerchantQrStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapGet("/product-images/{fileName}", (string fileName, ProductImageStore images, HttpContext context) =>
{
    var file = images.Find(fileName);
    if (file is null) return Results.NotFound();
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    return Results.File(file.Value.Path, file.Value.Type);
}).AllowAnonymous();
app.MapRazorPages().WithStaticAssets();

app.Run();
