using AuraCafé.API.DTOs.Shifts;

namespace AuraCafé.API.Interfaces;

public interface IShiftReconciliationService
{
    Task<ShiftCashDto?> GetAsync(int shiftId, string userId, bool isAdmin);
    Task<ShiftCashDto> CloseAsync(int shiftId, string userId, CloseShiftCashDto request);
}
