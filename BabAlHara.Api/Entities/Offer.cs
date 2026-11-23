namespace BabAlHara.Api.Entities;

public class Offer
{
    public int Id { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
