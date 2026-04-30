namespace Moeltid.Services;

public static class TimeZoneHelper
{
    /// <summary>
    /// Converts a wall-clock DateTime (no TZ assumption) to UTC using the given IANA TZ ID.
    /// Falls back to treating the value as UTC if the ID is unknown.
    /// </summary>
    public static DateTimeOffset ToUtc(DateTime wallClock, string ianaTimeZoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
            var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, tz));
        }
        catch
        {
            return new DateTimeOffset(wallClock, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Converts a UTC DateTimeOffset to a display string in the given IANA TZ.
    /// Falls back to UTC display if the ID is unknown.
    /// </summary>
    public static string ToLocalString(DateTimeOffset utc, string ianaTimeZoneId, string format = "f")
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
            return TimeZoneInfo.ConvertTime(utc, tz).ToString(format);
        }
        catch
        {
            return utc.ToString(format);
        }
    }
}
