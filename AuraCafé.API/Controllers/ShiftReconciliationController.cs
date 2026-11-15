using System.Security.Claims;
using AuraCafé.API.DTOs.Shifts;
using AuraCafé.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Controllers;

[ApiController, Authorize(Roles = "Admin,Cashier"), Route("api/shifts/{shiftId:int}/reconciliation")]
public class ShiftReconciliationController(IShiftReconciliationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int shiftId)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user is null) return Unauthorized();
        try
        {
            var result = await service.GetAsync(shiftId, user, User.IsInRole("Admin"));
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost, Authorize(Roles = "Cashier")]
    public async Task<IActionResult> Close(int shiftId, CloseShiftCashDto request)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user is null) return Unauthorized();
        try { return Ok(await service.CloseAsync(shiftId, user, request)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (Exception ex) when (ex is DbUpdateException || ex is SqliteException)
        {
            return Conflict(new { message = "Could not confirm shift closure. GET this reconciliation before trying again." });
        }
    }
}
