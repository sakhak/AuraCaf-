
using AuraCafé.API.Data;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public OrderRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Order order)
    {
        await _context.Orders.AddAsync(order);
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(x => x.SoldByStaff)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Order>> GetByStaffAsync(int staffId)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(x => x.SoldByStaff)
            .Include(x => x.Items)
            .Where(x => x.SoldByStaffId == staffId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync();
    }

    public async Task<List<Order>> GetAllAsync()
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(x => x.SoldByStaff)
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
