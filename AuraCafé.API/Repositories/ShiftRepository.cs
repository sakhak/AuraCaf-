
using AuraCafé.API.Data;
using AuraCafé.API.Interfaces.Shifts;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Repositories;

public class ShiftRepository : IShiftRepository
{
    private readonly AppDbContext _context;

    public ShiftRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WorkShift?> GetOpenShiftAsync(int staffId)
    {
        return await _context.WorkShifts
            .Include(x => x.Staff)
            .FirstOrDefaultAsync(x =>
                x.StaffId == staffId &&
                x.ClockOutUtc == null);
    }

    public async Task<WorkShift?> GetByIdAsync(int id)
    {
        return await _context.WorkShifts
            .Include(x => x.Staff)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<WorkShift>> GetByStaffAsync(int staffId)
    {
        return await _context.WorkShifts
            .AsNoTracking()
            .Include(x => x.Staff)
            .Where(x => x.StaffId == staffId)
            .OrderByDescending(x => x.ClockInUtc)
            .ToListAsync();
    }

    public async Task<List<WorkShift>> GetAllAsync()
    {
        return await _context.WorkShifts
            .AsNoTracking()
            .Include(x => x.Staff)
            .OrderByDescending(x => x.ClockInUtc)
            .Take(200)
            .ToListAsync();
    }

    public async Task AddAsync(WorkShift shift)
    {
        await _context.WorkShifts.AddAsync(shift);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
