
using AuraCafé.API.Models;

namespace AuraCafé.API.Interfaces.StaffManagement;

public interface IStaffRepository
{
    Task<List<Staff>> GetAllAsync();

    Task<Staff?> GetByIdAsync(int id);

    Task<bool> EmailExistsAsync(string email);

    Task AddAsync(Staff staff);

    Task SaveChangesAsync();
}
