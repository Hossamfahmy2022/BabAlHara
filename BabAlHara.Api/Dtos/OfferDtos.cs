namespace BabAlHara.Api.Dtos;

public record OfferDto(
    int Id,
    string TitleAr,
    string DescriptionAr,
    string? ImageUrl,
    bool IsActive,
    int SortOrder);

public record UpsertOfferRequest(
    string TitleAr,
    string DescriptionAr,
    string? ImageUrl,
    bool IsActive,
    int SortOrder);
