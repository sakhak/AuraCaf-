using Microsoft.AspNetCore.Mvc;

namespace AuraCafé.API.Controllers;

[ApiController]
[Route("api/cafe")]
public class CafeController : ControllerBase
{
    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            Name = "Auraaa Café",
            Address = "Phnom Penh, Cambodia",
            BaseCurrency = "USD",
            SupportedCurrencies = new[] { "USD", "KHR" },
            Status = "Open"
        });
    }
}