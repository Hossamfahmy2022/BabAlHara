using BabAlHara.Api.Dtos;
using BabAlHara.Api.Services;

namespace BabAlHara.Api.Tests;

public class CairoClockServiceTests
{
    private readonly CairoClockService _clock = new();

    private static readonly List<OpeningHourDto> RealHours =
    [
        new(0, "الأحد", "09:00", "03:00"),
        new(1, "الاثنين", "09:00", "03:30"),
        new(2, "الثلاثاء", "09:00", "03:00"),
        new(3, "الأربعاء", "09:00", "02:00"),
        new(4, "الخميس", "09:00", "02:00"),
        new(5, "الجمعة", "09:00", "03:00"),
        new(6, "السبت", "09:00", "03:00")
    ];

    private static DateTime CairoLocalToUtc(int year, int month, int day, int hour, int minute)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }

        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    [Fact]
    public void IsOpenNow_DuringDaytimeShift_ReturnsTrue()
    {
        // 2026-11-22 is Sunday (day 0), 14:00 Cairo
        var utc = CairoLocalToUtc(2026, 11, 22, 14, 0);
        Assert.True(_clock.IsOpenNow(RealHours, utc));
    }

    [Fact]
    public void IsOpenNow_AfterMidnightBeforeYesterdayClose_ReturnsTrue()
    {
        // 2026-11-23 is Monday 01:30 Cairo; Sunday closes at 03:00
        var utc = CairoLocalToUtc(2026, 11, 23, 1, 30);
        Assert.True(_clock.IsOpenNow(RealHours, utc));
    }

    [Fact]
    public void IsOpenNow_MondayExtendedShiftAt0315Tuesday_ReturnsTrue()
    {
        // 2026-11-24 is Tuesday 03:15 Cairo; Monday closes at 03:30
        var utc = CairoLocalToUtc(2026, 11, 24, 3, 15);
        Assert.True(_clock.IsOpenNow(RealHours, utc));
    }

    [Fact]
    public void IsOpenNow_EarlyMorningBetweenCloseAndOpen_ReturnsFalse()
    {
        // 2026-11-23 is Monday 04:00 Cairo (Sunday closed at 03:00, Monday opens at 09:00)
        var utc = CairoLocalToUtc(2026, 11, 23, 4, 0);
        Assert.False(_clock.IsOpenNow(RealHours, utc));
    }

    [Fact]
    public void IsOpenNow_Thursday0230AfterWednesday0200Close_ReturnsFalse()
    {
        // 2026-11-26 is Thursday 02:30 Cairo; Wednesday closed at 02:00
        var utc = CairoLocalToUtc(2026, 11, 26, 2, 30);
        Assert.False(_clock.IsOpenNow(RealHours, utc));
    }
}
