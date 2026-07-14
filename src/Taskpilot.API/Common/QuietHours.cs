namespace Taskpilot.API.Common;

/// <summary>
/// Decides whether a moment falls inside a user's quiet window. The window is given in
/// the user's local hours and may wrap past midnight (the usual case, e.g. 22:00–08:00).
/// </summary>
public static class QuietHours
{
    /// <summary>
    /// True when <paramref name="utcNow"/>, seen in the user's time zone, sits inside the
    /// [start, end) window. An empty window (start == end) is never quiet.
    /// </summary>
    /// <param name="startHour">Local hour the window opens (0–23).</param>
    /// <param name="endHour">Local hour the window closes (0–23).</param>
    /// <param name="timeZoneId">IANA id (e.g. "Europe/Kyiv"); null or unknown means UTC.</param>
    /// <param name="utcNow">The moment to test, in UTC.</param>
    public static bool IsQuiet(int startHour, int endHour, string? timeZoneId, DateTime utcNow)
    {
        // A zero-length window means the user is never in quiet hours.
        if (startHour == endHour)
            return false;

        var hour = ToLocal(utcNow, timeZoneId).Hour;

        // 22 → 8 wraps past midnight: quiet late at night OR early in the morning.
        return startHour > endHour
            ? hour >= startHour || hour < endHour
            : hour >= startHour && hour < endHour;
    }

    /// <summary>Converts a UTC moment into the user's local time, falling back to UTC.</summary>
    private static DateTime ToLocal(DateTime utcNow, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return utcNow;

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // An unknown id must not silence notifications by accident — treat it as UTC.
            return utcNow;
        }
    }

    /// <summary>True when the hour is a valid clock hour (0–23).</summary>
    public static bool IsValidHour(int hour) => hour is >= 0 and <= 23;

    /// <summary>True when the id names a time zone this machine knows (null counts as UTC).</summary>
    public static bool IsKnownTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return true;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}
