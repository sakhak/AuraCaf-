using Microsoft.AspNetCore.Mvc;

namespace AuraCafé.API.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Message = "CafeFlow API is working!",
            Status = "Success"
        });
    }
}