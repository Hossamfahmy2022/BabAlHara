using BabAlHara.Api.Dtos;
using BabAlHara.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BabAlHara.Api.Services;

public interface IOrderNotifier
{
    Task NotifyOrderCreatedAsync(OrderDto order, CancellationToken ct = default);
    Task NotifyOrderUpdatedAsync(OrderDto order, CancellationToken ct = default);
}

public class SignalROrderNotifier(IHubContext<OrdersHub> hubContext) : IOrderNotifier
{
    public Task NotifyOrderCreatedAsync(OrderDto order, CancellationToken ct = default)
    {
        return hubContext.Clients.All.SendAsync("OrderCreated", order, ct);
    }

    public Task NotifyOrderUpdatedAsync(OrderDto order, CancellationToken ct = default)
    {
        return hubContext.Clients.All.SendAsync("OrderUpdated", order, ct);
    }
}
