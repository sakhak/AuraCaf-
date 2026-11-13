using Microsoft.EntityFrameworkCore;
using AuraCafé.API.Data;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Repositories;
using AuraCafé.API.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")));

builder.Services.AddScoped<
    IProductRepository, ProductRepository>();

builder.Services.AddScoped<
    IProductService, ProductService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
   
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
