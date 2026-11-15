using AuraCafé.API.DTOs.Reports;
using AuraCafé.API.Interfaces;

namespace AuraCafé.API.Services;

public class ReportingService(IReportingRepository repository) : IReportingService
{
    public static TimeZoneInfo BusinessZone => TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessZone));

    public Task<SalesReportDto> GetAsync(DateOnly from, DateOnly to)
    {
        if (from > to || to == DateOnly.MaxValue || to.DayNumber - from.DayNumber > 365)
            throw new ArgumentException("Choose a valid date range of at most 366 days.");
        var start = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), BusinessZone);
        var end = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), BusinessZone);
        return repository.ReadAsync(from, to, start, end);
    }
}
