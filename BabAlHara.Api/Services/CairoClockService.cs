using System.Globalization;
using BabAlHara.Api.Dtos;

namespace BabAlHara.Api.Services;

public class CairoClockService
{
    private readonly TimeZoneInfo _cairoTz;

    public CairoClockService()
    {
        _cairoTz = ResolveCairoTimeZone();
    }

    private static TimeZoneInfo ResolveCairoTimeZone()
    {
        foreach (var id in new[] { "Africa/Cairo", "Egypt Standard Time" })
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

        return TimeZoneInfo.CreateCustomTimeZone("Africa/Cairo", TimeSpan.FromHours(2), "Cairo", "Cairo");
    }

    public DateTime ToCairoTime(DateTime utc)
    {
        var normalized = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(normalized, _cairoTz);
    }

    public DateOnly TodayCairo(DateTime? utcNow = null)
    {
        return DateOnly.FromDateTime(ToCairoTime(utcNow ?? DateTime.UtcNow));
    }

    public (DateTime StartUtc, DateTime EndUtcExclusive) GetUtcRangeForCairoDates(DateOnly from, DateOnly to)
    {
        var startLocal = from.ToDateTime(TimeOnly.MinValue);
        var endLocal = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        if (_cairoTz.IsInvalidTime(startLocal))
        {
            startLocal = startLocal.AddHours(1);
        }

        if (_cairoTz.IsInvalidTime(endLocal))
        {
            endLocal = endLocal.AddHours(1);
        }

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, _cairoTz);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, _cairoTz);
        return (startUtc, endUtc);
    }

    public bool IsOpenNow(IReadOnlyList<OpeningHourDto> hours, DateTime utcNow)
    {
        if (hours.Count == 0)
        {
            return false;
        }

        var localNow = ToCairoTime(utcNow);
        var todayDow = (int)localNow.DayOfWeek;
        var yesterdayDow = (todayDow + 6) % 7;
        var nowTime = TimeOnly.FromDateTime(localNow);

        var todayEntry = hours.FirstOrDefault(h => h.DayOfWeek == todayDow);
        if (todayEntry is not null && TryParseShift(todayEntry, out var openToday, out var closeToday))
        {
            if (closeToday > openToday)
            {
                if (nowTime >= openToday && nowTime < closeToday)
                {
                    return true;
                }
            }
            else if (closeToday < openToday)
            {
                if (nowTime >= openToday)
                {
                    return true;
                }
            }
        }

        var yesterdayEntry = hours.FirstOrDefault(h => h.DayOfWeek == yesterdayDow);
        if (yesterdayEntry is not null && TryParseShift(yesterdayEntry, out var openYesterday, out var closeYesterday))
        {
            if (closeYesterday < openYesterday && nowTime < closeYesterday)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseShift(OpeningHourDto entry, out TimeOnly open, out TimeOnly close)
    {
        var openOk = TimeOnly.TryParseExact(entry.Open, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out open);
        var closeOk = TimeOnly.TryParseExact(entry.Close, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out close);
        return openOk && closeOk;
    }
}
