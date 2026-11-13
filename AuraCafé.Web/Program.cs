
using AuraCafé.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Razor Pages
builder.Services.AddRazorPages();

// 2. Configure named HttpClient for Aura Café API
builder.Services.AddHttpClient("AuraCaféAPI", client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException(
            "API BaseUrl is not configured.");
    }

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// 3. Register CafeApiService
builder.Services.AddTransient<CafeApiService>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();

    return new CafeApiService(
        factory.CreateClient("AuraCaféAPI"));
});

// 4. Register ProductApiService (NEW)
builder.Services.AddTransient<ProductApiService>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();

    return new ProductApiService(
        factory.CreateClient("AuraCaféAPI"));
});

var app = builder.Build();

// 5. Configure HTTP middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

// 6. Map static assets and Razor Pages
app.MapStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
