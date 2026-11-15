
using AuraCafé.API.DTOs.Orders;

namespace AuraCafé.API.Interfaces;

public interface IOrderService
{
    Task<OrderResponseDto> CreateAsync(
        string userId,
        CreateOrderDto dto);

    Task<OrderResponseDto?> GetByIdAsync(
        int id,
        string userId,
        bool isAdmin);

    Task<List<OrderResponseDto>> GetMyOrdersAsync(
        string userId);

    Task<List<OrderResponseDto>> GetAllAsync();
}
