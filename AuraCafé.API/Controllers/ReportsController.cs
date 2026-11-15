using AuraCafé.API.Interfaces;
using AuraCafé.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCafé.API.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/reports")]
public class ReportsController(IReportingService reporting) : ControllerBase
{
    [HttpGet("sales")]
    public async Task<IActionResult> Sales(DateOnly? from, DateOnly? to)
    {
        try { return Ok(await reporting.GetAsync(from ?? ReportingService.Today, to ?? ReportingService.Today)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
