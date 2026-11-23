namespace BabAlHara.Api.Dtos;

public record TopItemDto(
    string ItemNameAr,
    int Quantity,
    decimal Revenue);

public record SalesByDayDto(
    string Date,
    int Orders,
    decimal Revenue);

public record SalesByHourDto(
    int Hour,
    int Orders);

public record DashboardDto(
    int OrdersCount,
    decimal Revenue,
    decimal AverageOrderValue,
    int CancelledCount,
    int PendingCount,
    Dictionary<string, int> ByStatus,
    Dictionary<string, int> ByChannel,
    List<TopItemDto> TopItems,
    List<SalesByDayDto> SalesByDay,
    List<SalesByHourDto> SalesByHour);
