namespace BabAlHara.Api.Entities;

public class MenuItem
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public MenuCategory Category { get; set; } = null!;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Internal admin-only reference from the 2020 menu. Never expose on public endpoints.
    /// </summary>
    public decimal? LastKnownPrice2020 { get; set; }

    public ICollection<MenuItemVariant> Variants { get; set; } = new List<MenuItemVariant>();
}
