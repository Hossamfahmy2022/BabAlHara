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
[Route("api/admin")]
public class AdminMenuController(AppDbContext db) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<List<MenuCategoryDto>>> GetCategories(CancellationToken ct)
    {
        var categories = await db.MenuCategories
            .AsNoTracking()
            .Include(c => c.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Variants.Where(v => !v.IsDeleted))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        return Ok(categories
            .Select(c => c.ToDto(includeUnavailable: true, includeAdminReference: true))
            .ToList());
    }

    [HttpPost("categories")]
    public async Task<ActionResult<MenuCategoryDto>> CreateCategory(
        [FromBody] UpsertCategoryRequest request,
        CancellationToken ct)
    {
        var slug = NormalizeSlug(request.Slug);
        var nameAr = request.NameAr?.Trim() ?? string.Empty;

        ValidateCategory(slug, nameAr);

        if (await db.MenuCategories.AnyAsync(c => c.Slug == slug, ct))
        {
            throw new DomainConflictException("يوجد قسم آخر بنفس المعرف (Slug).");
        }

        var category = new MenuCategory
        {
            Slug = slug,
            NameAr = nameAr,
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            SortOrder = request.SortOrder
        };

        db.MenuCategories.Add(category);
        await db.SaveChangesAsync(ct);

        return Created($"/api/admin/categories/{category.Id}", category.ToDto(true, true));
    }

    [HttpPut("categories/{id:int}")]
    public async Task<ActionResult<MenuCategoryDto>> UpdateCategory(
        int id,
        [FromBody] UpsertCategoryRequest request,
        CancellationToken ct)
    {
        var category = await db.MenuCategories
            .Include(c => c.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Variants.Where(v => !v.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("القسم غير موجود.");

        var slug = NormalizeSlug(request.Slug);
        var nameAr = request.NameAr?.Trim() ?? string.Empty;
        ValidateCategory(slug, nameAr);

        if (await db.MenuCategories.AnyAsync(c => c.Slug == slug && c.Id != id, ct))
        {
            throw new DomainConflictException("يوجد قسم آخر بنفس المعرف (Slug).");
        }

        category.Slug = slug;
        category.NameAr = nameAr;
        category.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        category.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        category.SortOrder = request.SortOrder;

        await db.SaveChangesAsync(ct);
        return Ok(category.ToDto(true, true));
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        var category = await db.MenuCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("القسم غير موجود.");

        var hasItems = await db.MenuItems.AnyAsync(i => i.CategoryId == id && !i.IsDeleted, ct);
        if (hasItems)
        {
            throw new DomainConflictException("لا يمكن حذف القسم لوجود أصناف بداخله. انقل الأصناف أو احذفها أولاً.");
        }

        // Remove soft-deleted items in this category first if any exist without orders, or detach
        var deletedItems = await db.MenuItems.Where(i => i.CategoryId == id && i.IsDeleted).ToListAsync(ct);
        if (deletedItems.Count > 0)
        {
            throw new DomainConflictException("لا يمكن حذف القسم لارتباطه بأصناف في طلبات سابقة.");
        }

        db.MenuCategories.Remove(category);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("items")]
    public async Task<ActionResult<List<MenuItemDto>>> GetItems(
        [FromQuery] int? categoryId,
        CancellationToken ct)
    {
        var query = db.MenuItems
            .AsNoTracking()
            .Include(i => i.Variants.Where(v => !v.IsDeleted))
            .Where(i => !i.IsDeleted);

        if (categoryId.HasValue)
        {
            query = query.Where(i => i.CategoryId == categoryId.Value);
        }

        var items = await query
            .OrderBy(i => i.CategoryId)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .ToListAsync(ct);

        return Ok(items.Select(i => i.ToDto(includeAdminReference: true)).ToList());
    }

    [HttpPost("items")]
    public async Task<ActionResult<MenuItemDto>> CreateItem(
        [FromBody] UpsertMenuItemRequest request,
        CancellationToken ct)
    {
        await ValidateItemRequestAsync(request, ct);

        var item = new MenuItem
        {
            CategoryId = request.CategoryId,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            DescriptionAr = string.IsNullOrWhiteSpace(request.DescriptionAr) ? null : request.DescriptionAr.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            IsAvailable = request.IsAvailable,
            IsFeatured = request.IsFeatured,
            SortOrder = request.SortOrder
        };

        var orderIdx = 1;
        foreach (var v in request.Variants)
        {
            item.Variants.Add(new MenuItemVariant
            {
                NameAr = string.IsNullOrWhiteSpace(v.NameAr) ? null : v.NameAr.Trim(),
                Price = v.Price,
                SortOrder = v.SortOrder > 0 ? v.SortOrder : orderIdx
            });
            orderIdx++;
        }

        db.MenuItems.Add(item);
        await db.SaveChangesAsync(ct);

        return Created($"/api/admin/items/{item.Id}", item.ToDto(includeAdminReference: true));
    }

    [HttpPut("items/{id:int}")]
    public async Task<ActionResult<MenuItemDto>> UpdateItem(
        int id,
        [FromBody] UpsertMenuItemRequest request,
        CancellationToken ct)
    {
        var item = await db.MenuItems
            .Include(i => i.Variants)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct)
            ?? throw new KeyNotFoundException("الصنف غير موجود.");

        await ValidateItemRequestAsync(request, ct);

        item.CategoryId = request.CategoryId;
        item.NameAr = request.NameAr.Trim();
        item.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        item.DescriptionAr = string.IsNullOrWhiteSpace(request.DescriptionAr) ? null : request.DescriptionAr.Trim();
        item.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        item.IsAvailable = request.IsAvailable;
        item.IsFeatured = request.IsFeatured;
        item.SortOrder = request.SortOrder;

        var incomingIds = request.Variants
            .Where(v => v.Id.HasValue && v.Id.Value > 0)
            .Select(v => v.Id!.Value)
            .ToHashSet();

        foreach (var existing in item.Variants.Where(v => !v.IsDeleted))
        {
            if (!incomingIds.Contains(existing.Id))
            {
                existing.IsDeleted = true;
            }
        }

        var orderIdx = 1;
        foreach (var v in request.Variants)
        {
            var sort = v.SortOrder > 0 ? v.SortOrder : orderIdx;
            var vName = string.IsNullOrWhiteSpace(v.NameAr) ? null : v.NameAr.Trim();

            if (v.Id.HasValue && v.Id.Value > 0)
            {
                var existing = item.Variants.FirstOrDefault(x => x.Id == v.Id.Value);
                if (existing is not null)
                {
                    existing.NameAr = vName;
                    existing.Price = v.Price;
                    existing.SortOrder = sort;
                    existing.IsDeleted = false;
                }
                else
                {
                    item.Variants.Add(new MenuItemVariant
                    {
                        NameAr = vName,
                        Price = v.Price,
                        SortOrder = sort
                    });
                }
            }
            else
            {
                item.Variants.Add(new MenuItemVariant
                {
                    NameAr = vName,
                    Price = v.Price,
                    SortOrder = sort
                });
            }

            orderIdx++;
        }

        await db.SaveChangesAsync(ct);
        return Ok(item.ToDto(includeAdminReference: true));
    }

    [HttpPatch("items/{id:int}/availability")]
    public async Task<ActionResult<MenuItemDto>> UpdateAvailability(
        int id,
        [FromBody] UpdateAvailabilityRequest request,
        CancellationToken ct)
    {
        var item = await db.MenuItems
            .Include(i => i.Variants.Where(v => !v.IsDeleted))
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct)
            ?? throw new KeyNotFoundException("الصنف غير موجود.");

        if (request.IsAvailable)
        {
            var activeVariants = item.Variants.Where(v => !v.IsDeleted).ToList();
            if (activeVariants.Count == 0 || activeVariants.Any(v => v.Price is null or <= 0))
            {
                throw new DomainValidationException(
                    "isAvailable",
                    "لا يمكن تفعيل هذا الصنف قبل تحديد سعر صحيح لكل الخيارات.");
            }
        }

        item.IsAvailable = request.IsAvailable;
        await db.SaveChangesAsync(ct);

        return Ok(item.ToDto(includeAdminReference: true));
    }

    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> DeleteItem(int id, CancellationToken ct)
    {
        var item = await db.MenuItems
            .Include(i => i.Variants)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct)
            ?? throw new KeyNotFoundException("الصنف غير موجود.");

        var isReferenced = await db.OrderItems.AnyAsync(oi => oi.MenuItemId == id, ct);
        if (isReferenced)
        {
            item.IsDeleted = true;
            item.IsAvailable = false;
        }
        else
        {
            db.MenuItems.Remove(item);
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string NormalizeSlug(string? slug) =>
        (slug ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '-');

    private static void ValidateCategory(string slug, string nameAr)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 80)
        {
            errors["slug"] = ["يرجى إدخال معرف قسم صحيح (Slug)."];
        }
        if (string.IsNullOrWhiteSpace(nameAr) || nameAr.Length > 120)
        {
            errors["nameAr"] = ["يرجى إدخال اسم القسم بالعربية."];
        }
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }

    private async Task ValidateItemRequestAsync(UpsertMenuItemRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (!await db.MenuCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            errors["categoryId"] = ["القسم المختار غير موجود."];
        }

        if (string.IsNullOrWhiteSpace(request.NameAr) || request.NameAr.Trim().Length > 160)
        {
            errors["nameAr"] = ["اسم الصنف بالعربية مطلوب (بحد أقصى 160 حرفاً)."];
        }

        if (request.Variants is null || request.Variants.Count == 0)
        {
            errors["variants"] = ["يجب إضافة خيار أو سعر واحد على الأقل للصنف."];
        }
        else
        {
            if (request.Variants.Any(v => v.Price.HasValue && v.Price.Value <= 0))
            {
                errors["variants"] = ["السعر يجب أن يكون أكبر من صفر أو يُترك فارغاً إذا كان غير محدد."];
            }
            else if (request.IsAvailable && request.Variants.Any(v => v.Price is null or <= 0))
            {
                errors["isAvailable"] = ["لا يمكن تفعيل الصنف للعملاء قبل إدخال سعر صحيح لجميع الخيارات."];
            }
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }
}
