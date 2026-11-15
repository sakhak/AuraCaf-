
using AuraCafé.API.Models;

namespace AuraCafé.API.Interfaces.Shifts;

public interface IShiftRepository
{
    Task<WorkShift?> GetOpenShiftAsync(int staffId);

    Task<WorkShift?> GetByIdAsync(int id);

    Task<List<WorkShift>> GetByStaffAsync(int staffId);

    Task<List<WorkShift>> GetAllAsync();

    Task AddAsync(WorkShift shift);

    Task SaveChangesAsync();
}
