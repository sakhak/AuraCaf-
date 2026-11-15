using AuraCafé.API.Services.Bakong;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace AuraCafé.API.Controllers;

[ApiController]
[Route("api/payments/bakong-sandbox")]
[Authorize(Roles = "Cashier")]
public class BakongSandboxController(BakongSandboxService service) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    [HttpGet("orders/{orderId:int}")]
    public Task<IActionResult> Get(int orderId) => RunAsync(async () => (object?)await service.GetAsync(orderId, UserId!));
    [HttpPost("orders/{orderId:int}")]
    public Task<IActionResult> Start(int orderId, CancellationToken cancellationToken) => RunAsync(async () => await service.StartAsync(orderId, UserId!, cancellationToken));
    [HttpPost("orders/{orderId:int}/check")]
    public Task<IActionResult> Check(int orderId, CancellationToken cancellationToken) => RunAsync(async () => await service.CheckAsync(orderId, UserId!, cancellationToken));
    private async Task<IActionResult> RunAsync(Func<Task<object?>> action)
    {
        if (UserId is null) return Unauthorized();
        try { var result = await action(); return result is null ? NotFound() : Ok(result); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (BakongProviderException ex) { return StatusCode(503, new { message = ex.Message }); }
    }
}

[ApiController]
[Route("api/payments/bakong-sandbox/connection")]
[Authorize(Roles = "Admin")]
public class BakongSandboxConnectionController(IBakongSandboxClient provider, IOptions<BakongSandboxOptions> options) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        try
        {
            if (!options.Value.IsConfigured) return StatusCode(503, new { message = "Configure BakongSandbox:Enabled, ReceiverAccountId and ApiToken on the API server first." });
            var exists = await provider.AccountExistsAsync(cancellationToken);
            return Ok(new { environment = "Sandbox", accountExists = exists, receiverAccountId = options.Value.ReceiverAccountId });
        }
        catch (BakongProviderException ex) { return StatusCode(503, new { message = ex.Message }); }
    }
}
