
using AuraCafé.API.DTOs.Staff;

namespace AuraCafé.API.Interfaces.StaffManagement;

public interface IStaffService
{
    Task<List<StaffResponseDto>> GetAllAsync();

    Task<StaffResponseDto?> GetByIdAsync(int id);

    Task<StaffResponseDto> CreateAsync(CreateStaffDto dto);

    Task<bool> UpdateAsync(int id, UpdateStaffDto dto);

    Task<bool> SetStatusAsync(int id, bool active);
}
