namespace BabAlHara.Api.Entities;

public class RestaurantSetting
{
    public int Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string TaglineAr { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public string AddressAr { get; set; } = string.Empty;
    public string AddressEn { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string PlusCode { get; set; } = string.Empty;
    public string GoogleMapsUrl { get; set; } = string.Empty;
    public string PhonesJson { get; set; } = "[]";
    public string WhatsappNumber { get; set; } = string.Empty;
    public string? ComplaintsPhone { get; set; }
    public string FacebookUrl { get; set; } = string.Empty;
    public decimal DeliveryFee { get; set; }
    public decimal MinOrderAmount { get; set; }
    public bool AcceptingOrders { get; set; } = true;
    public string PaymentMethodsJson { get; set; } = "[]";
    public string ServicesJson { get; set; } = "[]";
    public string OpeningHoursJson { get; set; } = "[]";
    public string RatingsJson { get; set; } = "{}";
    public string LogoUrl { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public string MenuImageUrl { get; set; } = string.Empty;
}
