
using AuraCafé.API.DTOs.Shifts;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Interfaces.Shifts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuraCafé.API.Controllers;

[ApiController]
[Authorize]
[Route("api/shifts")]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _service;

    public ShiftsController(IShiftService service)
    {
        _service = service;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Cashier")]
    [HttpGet("my/current")]
    public async Task<IActionResult> GetCurrent()
    {
        if (CurrentUserId is null)
            return Unauthorized();

        try
        {
            var shift = await _service.GetCurrentAsync(
                CurrentUserId);

            return shift is null ? NoContent() : Ok(shift);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Cashier")]
    [HttpGet("my/history")]
    public async Task<IActionResult> GetMyHistory()
    {
        if (CurrentUserId is null)
            return Unauthorized();

        try
        {
            return Ok(await _service.GetMyHistoryAsync(
                CurrentUserId));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Cashier")]
    [HttpPost("clock-in")]
    public async Task<IActionResult> ClockIn([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ClockInCashDto? request)
    {
        if (CurrentUserId is null)
            return Unauthorized();

        try
        {
            var shift = await _service.ClockInAsync(
                CurrentUserId, request?.OpeningCashCents, request?.OpeningCashRiel);

            return Ok(shift);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Cashier")]
    [HttpPost("clock-out")]
    public async Task<IActionResult> ClockOut()
    {
        if (CurrentUserId is null)
            return Unauthorized();

        try
        {
            var shift = await _service.ClockOutAsync(
                CurrentUserId);

            return Ok(shift);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<ShiftResponseDto>>>
        GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }
}
