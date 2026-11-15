
using AuraCafé.API.DTOs.Payments;

namespace AuraCafé.API.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponseDto> CreateCashAsync(
        string userId,
        CreateCashPaymentDto dto);

    Task<PaymentResponseDto?> GetByIdAsync(int id);

    Task<PaymentResponseDto?> GetByOrderIdAsync(
        int orderId,
        string userId,
        bool isAdmin);

    Task<List<PaymentResponseDto>> GetAllAsync();
}
