
using AuraCafé.API.Models;

namespace AuraCafé.API.Interfaces;

public interface IOrderRepository
{
    Task AddAsync(Order order);

    Task<Order?> GetByIdAsync(int id);

    Task<List<Order>> GetByStaffAsync(int staffId);

    Task<List<Order>> GetAllAsync();

    Task SaveChangesAsync();
}
