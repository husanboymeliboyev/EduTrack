namespace EduTrack.Models;

public static class AppClock
{
    private static readonly Lazy<TimeZoneInfo> UzbekistanTimeZone = new(ResolveTimeZone);

    public static DateTime UtcNow => DateTime.UtcNow;

    public static DateTime UzbekistanNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, UzbekistanTimeZone.Value);

    public static DateTime ToUzbekistanTime(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, UzbekistanTimeZone.Value);
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "Asia/Tashkent", "West Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
