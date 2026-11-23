namespace BabAlHara.Api.Entities;

public class MenuItemVariant
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public string? NameAr { get; set; }
    public decimal? Price { get; set; }
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
}
