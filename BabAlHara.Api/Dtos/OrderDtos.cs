using BabAlHara.Api.Entities;

namespace BabAlHara.Api.Dtos;

public record CreateOrderItemRequest(
    int MenuItemId,
    int VariantId,
    int Quantity,
    string? Notes);

public record CreateOrderRequest(
    string CustomerName,
    string CustomerPhone,
    OrderType OrderType,
    string? Address,
    string? Notes,
    OrderChannel Channel,
    List<CreateOrderItemRequest> Items);

public record OrderItemDto(
    int Id,
    int MenuItemId,
    int VariantId,
    string ItemNameAr,
    string? VariantNameAr,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    string? Notes);

public record OrderStatusHistoryDto(
    OrderStatus Status,
    DateTime ChangedAt,
    string? Note);

public record OrderDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhone,
    OrderType OrderType,
    string? Address,
    string? Notes,
    OrderChannel Channel,
    OrderStatus Status,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<OrderItemDto> Items,
    List<OrderStatusHistoryDto> StatusHistory);

public record OrderTrackingDto(
    string OrderNumber,
    OrderStatus Status,
    OrderType OrderType,
    decimal Total,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<OrderStatusHistoryDto> StatusHistory,
    List<OrderItemDto> Items);

public record PagedResult<T>(
    List<T> Items,
    int Total,
    int Page,
    int PageSize);

public record UpdateOrderStatusRequest(
    OrderStatus Status,
    string? Note);
