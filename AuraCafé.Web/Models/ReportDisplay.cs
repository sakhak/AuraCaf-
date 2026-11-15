using System.Globalization;

namespace AuraCafé.Web.Models;

public static class ReportDisplay
{
    public static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));
    public static string Riel(long riel) => "KHR " + riel.ToString("N0", CultureInfo.GetCultureInfo("en-US"));
    public static string Money(long cents) => "USD " + (cents / 100m).ToString("N2", CultureInfo.GetCultureInfo("en-US"));
    public static string Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
}
