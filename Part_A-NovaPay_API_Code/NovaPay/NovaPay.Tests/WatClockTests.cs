using NovaPay.Api.Common;

namespace NovaPay.Tests;

public class WatClockTests
{
    [Fact]
    public void GetWatDayBoundsUtc_MidnightWatIsOneHourBeforeUtcMidnight()
    {
        // 2026-03-05T00:00:00 WAT (UTC+1) is 2026-03-04T23:00:00 UTC.
        var instant = new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(1));

        var (startUtc, endUtc) = WatClock.GetWatDayBoundsUtc(instant);

        Assert.Equal(new DateTime(2026, 3, 4, 23, 0, 0, DateTimeKind.Unspecified), startUtc);
        Assert.Equal(new DateTime(2026, 3, 5, 23, 0, 0, DateTimeKind.Unspecified), endUtc);
    }

    [Fact]
    public void GetWatDayBoundsUtc_JustBeforeWatMidnight_FallsInPreviousWatDay()
    {
        // 2026-03-04T23:59:59Z is 2026-03-05T00:59:59 WAT — the last second of the WAT day
        // that started at 2026-03-04T23:00:00Z, one hour before the corresponding UTC midnight.
        var instant = new DateTimeOffset(2026, 3, 4, 23, 59, 59, TimeSpan.Zero);

        var (startUtc, endUtc) = WatClock.GetWatDayBoundsUtc(instant);

        Assert.Equal(new DateTime(2026, 3, 4, 23, 0, 0, DateTimeKind.Unspecified), startUtc);
        Assert.Equal(new DateTime(2026, 3, 5, 23, 0, 0, DateTimeKind.Unspecified), endUtc);
    }

    [Fact]
    public void GetWatDayBoundsUtc_IsIndependentOfServerLocalTimeZone()
    {
        // WAT is a fixed UTC+1 offset with no DST, so bounds must be derivable purely from the
        // UTC instant — regardless of what TimeZoneInfo.Local happens to be on the host machine.
        var instantUtc = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        var (startUtc, endUtc) = WatClock.GetWatDayBoundsUtc(instantUtc);

        Assert.Equal(TimeSpan.FromHours(24), endUtc - startUtc);
        Assert.True(startUtc <= instantUtc.UtcDateTime && instantUtc.UtcDateTime < endUtc);
    }
}
