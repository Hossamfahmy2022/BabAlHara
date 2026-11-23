using BabAlHara.Api.Data;
using BabAlHara.Api.Dtos;
using BabAlHara.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BabAlHara.Api.Controllers;

[ApiController]
public class PublicController(
    AppDbContext db,
    CairoClockService clock,
    OrderService orderService) : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "ok", service = "BabAlHara.Api" });

    [HttpGet("/api/restaurant")]
    public async Task<ActionResult<RestaurantInfoDto>> GetRestaurant(CancellationToken ct)
    {
        var settings = await db.RestaurantSettings.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("إعدادات المطعم غير موجودة.");

        return Ok(settings.ToDto(clock));
    }

    [HttpGet("/api/menu")]
    public async Task<ActionResult<List<MenuCategoryDto>>> GetMenu(CancellationToken ct)
    {
        var categories = await db.MenuCategories
            .AsNoTracking()
            .Include(c => c.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Variants.Where(v => !v.IsDeleted))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        var result = categories
            .Select(c => c.ToDto(includeUnavailable: false, includeAdminReference: false))
            .Where(c => c.Items.Count > 0)
            .ToList();

        return Ok(result);
    }

    [HttpGet("/api/offers")]
    public async Task<ActionResult<List<OfferDto>>> GetOffers(CancellationToken ct)
    {
        var offers = await db.Offers
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Id)
            .Select(o => o.ToDto())
            .ToListAsync(ct);

        return Ok(offers);
    }

    [HttpPost("/api/orders")]
    public async Task<ActionResult<OrderDto>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        var order = await orderService.CreateOrderAsync(request, isAdminOrder: false, ct);
        return Created($"/api/orders/track?orderNumber={order.OrderNumber}&phone={order.CustomerPhone}", order);
    }

    [HttpGet("/api/orders/track")]
    public async Task<ActionResult<OrderTrackingDto>> TrackOrder(
        [FromQuery] string? orderNumber,
        [FromQuery] string? phone,
        CancellationToken ct)
    {
        var normalizedNumber = orderNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalizedPhone = OrderService.NormalizeDigits(phone);

        if (string.IsNullOrWhiteSpace(normalizedNumber) || string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return NotFound(new ProblemDetails
            {
                Title = "الطلب غير موجود",
                Detail = "يرجى إدخال رقم الطلب ورقم الهاتف بشكل صحيح.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(
                o => o.OrderNumber == normalizedNumber && o.CustomerPhone == normalizedPhone,
                ct);

        if (order is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "الطلب غير موجود",
                Detail = "لم يتم العثور على طلب مطابق لهذا الرقم ورقم الهاتف.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(order.ToTrackingDto());
    }
}
