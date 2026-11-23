namespace BabAlHara.Api.Entities;

public enum OrderStatus
{
    New,
    Confirmed,
    Preparing,
    ReadyForPickup,
    OutForDelivery,
    Delivered,
    Cancelled
}

public enum OrderType
{
    Delivery,
    Pickup
}

public enum OrderChannel
{
    Website,
    WhatsApp,
    Phone
}
