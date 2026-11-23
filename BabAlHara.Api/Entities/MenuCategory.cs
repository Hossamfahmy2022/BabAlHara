namespace BabAlHara.Api.Entities;

public class MenuCategory
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }

    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
