
using AuraCafé.API.Models;

namespace AuraCafé.API.Interfaces;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment);

    Task<Payment?> GetByIdAsync(int id);

    Task<Payment?> GetByOrderIdAsync(int orderId);

    Task<List<Payment>> GetAllAsync();

    Task SaveChangesAsync();
}
