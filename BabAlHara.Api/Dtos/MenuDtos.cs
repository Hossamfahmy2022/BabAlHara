using System.Text.Json.Serialization;

namespace BabAlHara.Api.Dtos;

public record VariantDto(
    int Id,
    string? NameAr,
    decimal? Price,
    int SortOrder);

public record MenuItemDto(
    int Id,
    int CategoryId,
    string NameAr,
    string? NameEn,
    string? DescriptionAr,
    string? ImageUrl,
    bool IsAvailable,
    bool IsFeatured,
    int SortOrder,
    bool NeedsPricing,
    List<VariantDto> Variants,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    decimal? LastKnownPrice2020 = null);

public record MenuCategoryDto(
    int Id,
    string Slug,
    string NameAr,
    string? NameEn,
    string? ImageUrl,
    int SortOrder,
    List<MenuItemDto> Items);

public record UpsertCategoryRequest(
    string Slug,
    string NameAr,
    string? NameEn,
    string? ImageUrl,
    int SortOrder);

public record UpsertVariantRequest(
    int? Id,
    string? NameAr,
    decimal? Price,
    int SortOrder);

public record UpsertMenuItemRequest(
    int CategoryId,
    string NameAr,
    string? NameEn,
    string? DescriptionAr,
    string? ImageUrl,
    bool IsAvailable,
    bool IsFeatured,
    int SortOrder,
    List<UpsertVariantRequest> Variants);

public record UpdateAvailabilityRequest(bool IsAvailable);
