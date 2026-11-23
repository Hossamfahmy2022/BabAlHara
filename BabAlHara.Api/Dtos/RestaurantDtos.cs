namespace BabAlHara.Api.Dtos;

public record OpeningHourDto(
    int DayOfWeek,
    string DayAr,
    string Open,
    string Close);

public record GoogleRatingDto(double Score, int Count);

public record FacebookRatingDto(int RecommendPercent, int Count);

public record RatingsDto(GoogleRatingDto Google, FacebookRatingDto Facebook);

public record RestaurantInfoDto(
    string NameAr,
    string NameEn,
    string TaglineAr,
    string DescriptionAr,
    string AddressAr,
    string AddressEn,
    double Latitude,
    double Longitude,
    string PlusCode,
    string GoogleMapsUrl,
    List<string> Phones,
    string WhatsappNumber,
    string? ComplaintsPhone,
    string FacebookUrl,
    decimal DeliveryFee,
    decimal MinOrderAmount,
    bool AcceptingOrders,
    bool IsOpenNow,
    List<string> PaymentMethods,
    List<string> Services,
    List<OpeningHourDto> OpeningHours,
    RatingsDto Ratings,
    string LogoUrl,
    string CoverUrl,
    string MenuImageUrl);

public record UpdateSettingsRequest(
    string NameAr,
    string NameEn,
    string TaglineAr,
    string DescriptionAr,
    string AddressAr,
    string AddressEn,
    double Latitude,
    double Longitude,
    string PlusCode,
    string GoogleMapsUrl,
    List<string> Phones,
    string WhatsappNumber,
    string? ComplaintsPhone,
    string FacebookUrl,
    decimal DeliveryFee,
    decimal MinOrderAmount,
    bool AcceptingOrders,
    List<string> PaymentMethods,
    List<string> Services,
    List<OpeningHourDto> OpeningHours,
    string LogoUrl,
    string CoverUrl,
    string MenuImageUrl);
