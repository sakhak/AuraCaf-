using AuraCafé.API.Auth;
using AuraCafé.API.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);

var app = builder.Build();

await app.ConfigureApiPipelineAsync();

app.Run();
