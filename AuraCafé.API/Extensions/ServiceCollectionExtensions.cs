using AuraCafé.API.Data;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Interfaces.Products;
using AuraCafé.API.Interfaces.Shifts;
using AuraCafé.API.Interfaces.StaffManagement;
using AuraCafé.API.Repositories;
using AuraCafé.API.Services;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuraCafé.API.Services.Bakong.BakongSandboxOptions>(configuration.GetSection("BakongSandbox"));
        services.AddHttpClient<AuraCafé.API.Services.Bakong.IBakongSandboxClient, AuraCafé.API.Services.Bakong.BakongSandboxClient>(client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<AuraCafé.API.Services.Bakong.BakongSandboxService>();
        services.AddControllers();
        services.AddOpenApi();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IShiftRepository, ShiftRepository>();
        services.AddScoped<IShiftService, ShiftService>();

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderService, OrderService>();

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IShiftReconciliationService, ShiftReconciliationService>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();




        return services;
    }
}
