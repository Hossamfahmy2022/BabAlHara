using BabAlHara.Api.Data;
using BabAlHara.Api.Dtos;
using BabAlHara.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BabAlHara.Api.Services;

public class DashboardService(AppDbContext db, CairoClockService clock)
{
    public async Task<DashboardDto> GetDashboardAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default)
    {
        var today = clock.TodayCairo();
        var from = fromDate ?? today;
        var to = toDate ?? today;

        if (from > to)
        {
            throw new DomainValidationException("from", "تاريخ البداية يجب أن يكون قبل أو مساوياً لتاريخ النهاية.");
        }

        var (startUtc, endUtcExclusive) = clock.GetUtcRangeForCairoDates(from, to);

        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CreatedAt >= startUtc && o.CreatedAt < endUtcExclusive)
            .ToListAsync(ct);

        var byStatus = Enum.GetNames<OrderStatus>().ToDictionary(name => name, _ => 0);
        var byChannel = Enum.GetNames<OrderChannel>().ToDictionary(name => name, _ => 0);

        foreach (var order in orders)
        {
            var statusKey = order.Status.ToString();
            byStatus[statusKey] = byStatus.GetValueOrDefault(statusKey) + 1;

            var channelKey = order.Channel.ToString();
            byChannel[channelKey] = byChannel.GetValueOrDefault(channelKey) + 1;
        }

        var cancelledCount = byStatus[OrderStatus.Cancelled.ToString()];
        var pendingCount = byStatus[OrderStatus.New.ToString()]
            + byStatus[OrderStatus.Confirmed.ToString()]
            + byStatus[OrderStatus.Preparing.ToString()];

        var nonCancelled = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        var revenue = nonCancelled.Sum(o => o.Total);
        var avg = nonCancelled.Count > 0 ? Math.Round(revenue / nonCancelled.Count, 2) : 0m;

        var topItems = nonCancelled
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ItemNameAr)
            .Select(g => new TopItemDto(
                g.Key,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.LineTotal)))
            .OrderByDescending(x => x.Quantity)
            .ThenByDescending(x => x.Revenue)
            .Take(10)
            .ToList();

        var dayBuckets = new Dictionary<DateOnly, (int Orders, decimal Revenue)>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            dayBuckets[d] = (0, 0m);
        }

        var hourCounts = Enumerable.Range(0, 24).ToDictionary(h => h, _ => 0);

        foreach (var order in nonCancelled)
        {
            var localDt = clock.ToCairoTime(order.CreatedAt);
            var localDate = DateOnly.FromDateTime(localDt);
            if (dayBuckets.TryGetValue(localDate, out var current))
            {
                dayBuckets[localDate] = (current.Orders + 1, current.Revenue + order.Total);
            }

            hourCounts[localDt.Hour] = hourCounts[localDt.Hour] + 1;
        }

        var salesByDay = dayBuckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new SalesByDayDto(
                kv.Key.ToString("yyyy-MM-dd"),
                kv.Value.Orders,
                kv.Value.Revenue))
            .ToList();

        var salesByHour = Enumerable.Range(0, 24)
            .Select(h => new SalesByHourDto(h, hourCounts[h]))
            .ToList();

        return new DashboardDto(
            orders.Count,
            revenue,
            avg,
            cancelledCount,
            pendingCount,
            byStatus,
            byChannel,
            topItems,
            salesByDay,
            salesByHour);
    }
}
