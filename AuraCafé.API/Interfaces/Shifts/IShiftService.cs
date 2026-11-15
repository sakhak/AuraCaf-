
using AuraCafé.API.DTOs.Shifts;

namespace AuraCafé.API.Interfaces.Shifts;

public interface IShiftService
{
    Task<ShiftResponseDto?> GetCurrentAsync(string userId);

    Task<List<ShiftResponseDto>> GetMyHistoryAsync(string userId);

    Task<ShiftResponseDto> ClockInAsync(string userId, int? openingCashCents = null, long? openingCashRiel = null);

    Task<ShiftResponseDto> ClockOutAsync(string userId);

    Task<List<ShiftResponseDto>> GetAllAsync();
}
