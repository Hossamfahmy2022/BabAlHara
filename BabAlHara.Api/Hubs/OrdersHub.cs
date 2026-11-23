using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BabAlHara.Api.Hubs;

[Authorize(Roles = "Admin")]
public class OrdersHub : Hub
{
}
