
using AuraCafé.API.DTOs.Staff;
using AuraCafé.API.Interfaces.StaffManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCafé.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/staff")]
public class StaffController : ControllerBase
{
    private readonly IStaffService _service;

    public StaffController(IStaffService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<StaffResponseDto>>>
        GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StaffResponseDto>>
        GetById(int id)
    {
        var staff = await _service.GetByIdAsync(id);

        if (staff is null)
            return NotFound();

        return Ok(staff);
    }

    [HttpPost]
    public async Task<ActionResult<StaffResponseDto>>
        Create(CreateStaffDto dto)
    {
        try
        {
            var staff = await _service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = staff.Id },
                staff);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id, UpdateStaffDto dto)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto);

            return updated ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(
        int id, ChangeStaffStatusDto dto)
    {
        var updated = await _service.SetStatusAsync(
            id, dto.IsActive);

        return updated ? NoContent() : NotFound();
    }
}

public class ChangeStaffStatusDto
{
    public bool IsActive { get; set; }
}
