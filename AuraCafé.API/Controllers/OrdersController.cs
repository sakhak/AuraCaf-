
using System.Security.Claims;
using AuraCafé.API.DTOs.Orders;
using AuraCafé.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuraCafé.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Cashier")]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _service;

    public OrdersController(IOrderService service)
    {
        _service = service;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    [Authorize(Roles = "Cashier")]
    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> Create(
        CreateOrderDto dto)
    {
        if (CurrentUserId is null)
            return Unauthorized();

        try
        {
            var order = await _service.CreateAsync(
                CurrentUserId, dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = order.Id },
                order);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponseDto>>
        GetById(int id)
    {
        if (CurrentUserId is null)
            return Unauthorized();

        var order = await _service.GetByIdAsync(
            id,
            CurrentUserId,
            User.IsInRole("Admin"));

        if (order is null)
            return NotFound();

        return Ok(order);
    }

    [Authorize(Roles = "Cashier")]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyOrders()
    {
        if (CurrentUserId is null)
            return Unauthorized();

        return Ok(await _service.GetMyOrdersAsync(
            CurrentUserId));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }
}
