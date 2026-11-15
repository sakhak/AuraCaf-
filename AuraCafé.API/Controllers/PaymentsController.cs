
using System.Security.Claims;
using AuraCafé.API.DTOs.Payments;
using AuraCafé.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCafé.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Cashier")]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _service;

    public PaymentsController(IPaymentService service)
    {
        _service = service;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Cashier")]
    [HttpPost("cash")]
    public async Task<ActionResult<PaymentResponseDto>>
        CreateCash(CreateCashPaymentDto dto)
    {
        if (CurrentUserId is null)
            return Unauthorized();

        try
        {
            var payment = await _service.CreateCashAsync(
                CurrentUserId, dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = payment.Id },
                payment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentResponseDto>>
        GetById(int id)
    {
        var payment = await _service.GetByIdAsync(id);

        return payment is null
            ? NotFound()
            : Ok(payment);
    }

    [HttpGet("order/{orderId:int}")]
    public async Task<ActionResult<PaymentResponseDto>>
        GetByOrderId(int orderId)
    {
        if (CurrentUserId is null)
            return Unauthorized();

        var payment = await _service.GetByOrderIdAsync(
            orderId,
            CurrentUserId,
            User.IsInRole("Admin"));

        return payment is null
            ? NotFound()
            : Ok(payment);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }
}
