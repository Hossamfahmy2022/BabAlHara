using BabAlHara.Api.Data;
using BabAlHara.Api.Dtos;
using BabAlHara.Api.Entities;
using BabAlHara.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BabAlHara.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/orders")]
public class AdminOrdersController(
    AppDbContext db,
    CairoClockService clock,
    OrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetOrders(
        [FromQuery] OrderStatus? status,
        [FromQuery] OrderChannel? channel,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (channel.HasValue)
        {
            query = query.Where(o => o.Channel == channel.Value);
        }

        if (from.HasValue || to.HasValue)
        {
            var effectiveFrom = from ?? DateOnly.MinValue.AddYears(2000);
            var effectiveTo = to ?? DateOnly.MaxValue.AddYears(-10);
            if (effectiveFrom > effectiveTo)
            {
                throw new DomainValidationException("from", "تاريخ البداية يجب أن يكون قبل تاريخ النهاية.");
            }

            if (from.HasValue)
            {
                var (startUtc, _) = clock.GetUtcRangeForCairoDates(from.Value, from.Value);
                query = query.Where(o => o.CreatedAt >= startUtc);
            }

            if (to.HasValue)
            {
                var (_, endUtcExclusive) = clock.GetUtcRangeForCairoDates(to.Value, to.Value);
                query = query.Where(o => o.CreatedAt < endUtcExclusive);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var normalizedDigits = OrderService.NormalizeDigits(term);
            query = query.Where(o =>
                o.OrderNumber.Contains(term)
                || o.CustomerName.Contains(term)
                || o.CustomerPhone.Contains(normalizedDigits));
        }

        var total = await query.CountAsync(ct);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<OrderDto>(
            orders.Select(o => o.ToDto()).ToList(),
            total,
            page,
            pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new KeyNotFoundException("الطلب غير موجود.");

        return Ok(order.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreatePhoneOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        var order = await orderService.CreateOrderAsync(request, isAdminOrder: true, ct);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        int id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        var updated = await orderService.UpdateStatusAsync(id, request.Status, request.Note, ct);
        return Ok(updated);
    }
}
