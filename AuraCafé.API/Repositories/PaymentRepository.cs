
using AuraCafé.API.Data;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _db;

    public PaymentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Payment payment)
    {
        await _db.Payments.AddAsync(payment);
    }

    public async Task<Payment?> GetByIdAsync(int id)
    {
        return await _db.Payments
            .AsNoTracking()
            .Include(x => x.Order)
            .Include(x => x.CollectedByStaff)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Payment?> GetByOrderIdAsync(int orderId)
    {
        return await _db.Payments
            .AsNoTracking()
            .Include(x => x.Order)
            .Include(x => x.CollectedByStaff)
            .FirstOrDefaultAsync(x => x.OrderId == orderId);
    }

    public async Task<List<Payment>> GetAllAsync()
    {
        return await _db.Payments
            .AsNoTracking()
            .Include(x => x.Order)
            .Include(x => x.CollectedByStaff)
            .OrderByDescending(x => x.PaidAtUtc)
            .Take(200)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
