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
[Route("api/admin/offers")]
public class AdminOffersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OfferDto>>> GetAll(CancellationToken ct)
    {
        var offers = await db.Offers
            .AsNoTracking()
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Id)
            .Select(o => o.ToDto())
            .ToListAsync(ct);

        return Ok(offers);
    }

    [HttpPost]
    public async Task<ActionResult<OfferDto>> Create(
        [FromBody] UpsertOfferRequest request,
        CancellationToken ct)
    {
        ValidateOffer(request);

        var offer = new Offer
        {
            TitleAr = request.TitleAr.Trim(),
            DescriptionAr = request.DescriptionAr.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

        db.Offers.Add(offer);
        await db.SaveChangesAsync(ct);

        return Created($"/api/admin/offers/{offer.Id}", offer.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OfferDto>> Update(
        int id,
        [FromBody] UpsertOfferRequest request,
        CancellationToken ct)
    {
        var offer = await db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new KeyNotFoundException("العرض غير موجود.");

        ValidateOffer(request);

        offer.TitleAr = request.TitleAr.Trim();
        offer.DescriptionAr = request.DescriptionAr.Trim();
        offer.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        offer.IsActive = request.IsActive;
        offer.SortOrder = request.SortOrder;

        await db.SaveChangesAsync(ct);
        return Ok(offer.ToDto());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var offer = await db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new KeyNotFoundException("العرض غير موجود.");

        db.Offers.Remove(offer);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void ValidateOffer(UpsertOfferRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.TitleAr))
        {
            errors["titleAr"] = ["عنوان العرض مطلوب."];
        }
        if (string.IsNullOrWhiteSpace(request.DescriptionAr))
        {
            errors["descriptionAr"] = ["وصف العرض مطلوب."];
        }
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }
}
