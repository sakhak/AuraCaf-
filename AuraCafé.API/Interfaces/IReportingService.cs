using AuraCafé.API.DTOs.Reports;

namespace AuraCafé.API.Interfaces;

public interface IReportingService
{
    Task<SalesReportDto> GetAsync(DateOnly from, DateOnly to);
}

public interface IReportingRepository
{
    Task<SalesReportDto> ReadAsync(DateOnly from, DateOnly to, DateTime startUtc, DateTime endUtc);
}
