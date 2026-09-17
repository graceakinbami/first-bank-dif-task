namespace NovaPay.Api.Common;

/// <summary>
/// West Africa Time (WAT, Africa/Lagos) is a fixed UTC+1 offset year-round — Nigeria does not
/// observe daylight saving. Rather than depend on the host OS's timezone database (which differs
/// between Windows and Linux, and may not even carry a Lagos entry), the offset is hardcoded here.
/// This lets "resets at midnight WAT" behave identically no matter where the API is deployed.
/// </summary>
public static class WatClock
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(1);

    public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public static DateTimeOffset NowInWat => UtcNow.ToOffset(Offset);

    /// <summary>
    /// The UTC [start, end) bounds of the WAT calendar day containing <paramref name="instantUtc"/>
    /// (defaults to now). Use this to window a query by "the current WAT day" regardless of server TZ.
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) GetWatDayBoundsUtc(DateTimeOffset? instantUtc = null)
    {
        var watNow = (instantUtc ?? UtcNow).ToOffset(Offset);
        var watDayStart = new DateTimeOffset(watNow.Year, watNow.Month, watNow.Day, 0, 0, 0, Offset);
        var watDayEnd = watDayStart.AddDays(1);
        return (watDayStart.UtcDateTime, watDayEnd.UtcDateTime);
    }
}
