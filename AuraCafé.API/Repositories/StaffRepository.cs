
using AuraCafé.API.Data;
using AuraCafé.API.Interfaces.StaffManagement;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Repositories;

public class StaffRepository : IStaffRepository
{
    private readonly AppDbContext _context;

    public StaffRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Staff>> GetAllAsync()
    {
        return await _context.StaffMembers
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Staff?> GetByIdAsync(int id)
    {
        return await _context.StaffMembers
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.StaffMembers
            .AnyAsync(x => x.Email == email);
    }

    public async Task AddAsync(Staff staff)
    {
        await _context.StaffMembers.AddAsync(staff);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
