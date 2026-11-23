using System.Text.Json;
using BabAlHara.Api.Data;
using BabAlHara.Api.Dtos;
using BabAlHara.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BabAlHara.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminSettingsController(
    AppDbContext db,
    CairoClockService clock,
    IWebHostEnvironment env) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    [HttpGet("settings")]
    public async Task<ActionResult<RestaurantInfoDto>> GetSettings(CancellationToken ct)
    {
        var settings = await db.RestaurantSettings.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("إعدادات المطعم غير موجودة.");

        return Ok(settings.ToDto(clock));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<RestaurantInfoDto>> UpdateSettings(
        [FromBody] UpdateSettingsRequest request,
        CancellationToken ct)
    {
        var settings = await db.RestaurantSettings.FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("إعدادات المطعم غير موجودة.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.NameAr))
        {
            errors["nameAr"] = ["اسم المطعم بالعربية مطلوب."];
        }
        if (request.DeliveryFee < 0)
        {
            errors["deliveryFee"] = ["رسوم التوصيل لا يمكن أن تكون سالبة."];
        }
        if (request.MinOrderAmount < 0)
        {
            errors["minOrderAmount"] = ["الحد الأدنى للطلب لا يمكن أن يكون سالباً."];
        }
        if (request.Phones is null || request.Phones.Count == 0 || request.Phones.All(string.IsNullOrWhiteSpace))
        {
            errors["phones"] = ["يجب إدخال رقم هاتف واحد على الأقل."];
        }
        if (string.IsNullOrWhiteSpace(request.WhatsappNumber))
        {
            errors["whatsappNumber"] = ["رقم الواتساب مطلوب."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        settings.NameAr = request.NameAr.Trim();
        settings.NameEn = (request.NameEn ?? string.Empty).Trim();
        settings.TaglineAr = (request.TaglineAr ?? string.Empty).Trim();
        settings.DescriptionAr = (request.DescriptionAr ?? string.Empty).Trim();
        settings.AddressAr = (request.AddressAr ?? string.Empty).Trim();
        settings.AddressEn = (request.AddressEn ?? string.Empty).Trim();
        settings.Latitude = request.Latitude;
        settings.Longitude = request.Longitude;
        settings.PlusCode = (request.PlusCode ?? string.Empty).Trim();
        settings.GoogleMapsUrl = (request.GoogleMapsUrl ?? string.Empty).Trim();
        settings.PhonesJson = JsonSerializer.Serialize(
            request.Phones!.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList(),
            JsonOptions);
        settings.WhatsappNumber = OrderService.NormalizeDigits(request.WhatsappNumber);
        settings.ComplaintsPhone = string.IsNullOrWhiteSpace(request.ComplaintsPhone) ? null : request.ComplaintsPhone.Trim();
        settings.FacebookUrl = (request.FacebookUrl ?? string.Empty).Trim();
        settings.DeliveryFee = request.DeliveryFee;
        settings.MinOrderAmount = request.MinOrderAmount;
        settings.AcceptingOrders = request.AcceptingOrders;
        settings.PaymentMethodsJson = JsonSerializer.Serialize(request.PaymentMethods ?? [], JsonOptions);
        settings.ServicesJson = JsonSerializer.Serialize(request.Services ?? [], JsonOptions);
        settings.OpeningHoursJson = JsonSerializer.Serialize(request.OpeningHours ?? [], JsonOptions);
        settings.LogoUrl = (request.LogoUrl ?? string.Empty).Trim();
        settings.CoverUrl = (request.CoverUrl ?? string.Empty).Trim();
        settings.MenuImageUrl = (request.MenuImageUrl ?? string.Empty).Trim();

        await db.SaveChangesAsync(ct);
        return Ok(settings.ToDto(clock));
    }

    [HttpPost("uploads")]
    [RequestSizeLimit(MaxUploadBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainValidationException("file", "يرجى اختيار صورة صالحة للرفع.");
        }

        if (file.Length > MaxUploadBytes)
        {
            throw new DomainValidationException("file", "حجم الصورة يجب ألا يتجاوز 5 ميجابايت.");
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new DomainValidationException("file", "صيغة الصورة المدعومة هي JPG أو PNG أو WEBP فقط.");
        }

        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;

        var uploadsDir = Path.Combine(webRoot, "images", "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsDir, fileName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream, ct);

        return Ok(new { url = $"/images/uploads/{fileName}" });
    }
}
