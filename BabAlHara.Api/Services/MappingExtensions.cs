using System.Text.Json;
using BabAlHara.Api.Dtos;
using BabAlHara.Api.Entities;

namespace BabAlHara.Api.Services;

public static class MappingExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RestaurantInfoDto ToDto(this RestaurantSetting s, CairoClockService clock, DateTime? utcNow = null)
    {
        var phones = DeserializeOrDefault<List<string>>(s.PhonesJson, []);
        var paymentMethods = DeserializeOrDefault<List<string>>(s.PaymentMethodsJson, []);
        var services = DeserializeOrDefault<List<string>>(s.ServicesJson, []);
        var hours = DeserializeOrDefault<List<OpeningHourDto>>(s.OpeningHoursJson, []);
        var ratings = DeserializeOrDefault(s.RatingsJson, new RatingsDto(new GoogleRatingDto(0, 0), new FacebookRatingDto(0, 0)));

        var isOpenNow = clock.IsOpenNow(hours, utcNow ?? DateTime.UtcNow);

        return new RestaurantInfoDto(
            s.NameAr,
            s.NameEn,
            s.TaglineAr,
            s.DescriptionAr,
            s.AddressAr,
            s.AddressEn,
            s.Latitude,
            s.Longitude,
            s.PlusCode,
            s.GoogleMapsUrl,
            phones,
            s.WhatsappNumber,
            s.ComplaintsPhone,
            s.FacebookUrl,
            s.DeliveryFee,
            s.MinOrderAmount,
            s.AcceptingOrders,
            isOpenNow,
            paymentMethods,
            services,
            hours,
            ratings,
            s.LogoUrl,
            s.CoverUrl,
            s.MenuImageUrl);
    }

    public static MenuItemDto ToDto(this MenuItem item, bool includeAdminReference = false)
    {
        var activeVariants = item.Variants
            .Where(v => !v.IsDeleted)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Id)
            .Select(v => new VariantDto(v.Id, v.NameAr, v.Price, v.SortOrder))
            .ToList();

        var needsPricing = activeVariants.Count == 0 || activeVariants.Any(v => v.Price is null or <= 0);

        return new MenuItemDto(
            item.Id,
            item.CategoryId,
            item.NameAr,
            item.NameEn,
            item.DescriptionAr,
            item.ImageUrl,
            item.IsAvailable,
            item.IsFeatured,
            item.SortOrder,
            needsPricing,
            activeVariants,
            includeAdminReference ? item.LastKnownPrice2020 : null);
    }

    public static MenuCategoryDto ToDto(this MenuCategory category, bool includeUnavailable, bool includeAdminReference = false)
    {
        var itemsQuery = category.Items
            .Where(i => !i.IsDeleted);

        if (!includeUnavailable)
        {
            itemsQuery = itemsQuery.Where(i =>
                i.IsAvailable
                && i.Variants.Any(v => !v.IsDeleted)
                && i.Variants.Where(v => !v.IsDeleted).All(v => v.Price is > 0));
        }

        var items = itemsQuery
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .Select(i => i.ToDto(includeAdminReference))
            .ToList();

        return new MenuCategoryDto(
            category.Id,
            category.Slug,
            category.NameAr,
            category.NameEn,
            category.ImageUrl,
            category.SortOrder,
            items);
    }

    public static OfferDto ToDto(this Offer offer)
    {
        return new OfferDto(
            offer.Id,
            offer.TitleAr,
            offer.DescriptionAr,
            offer.ImageUrl,
            offer.IsActive,
            offer.SortOrder);
    }

    public static OrderDto ToDto(this Order order)
    {
        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            order.CustomerPhone,
            order.OrderType,
            order.Address,
            order.Notes,
            order.Channel,
            order.Status,
            order.Subtotal,
            order.DeliveryFee,
            order.Total,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items
                .OrderBy(i => i.Id)
                .Select(i => new OrderItemDto(
                    i.Id,
                    i.MenuItemId,
                    i.VariantId,
                    i.ItemNameAr,
                    i.VariantNameAr,
                    i.UnitPrice,
                    i.Quantity,
                    i.LineTotal,
                    i.Notes))
                .ToList(),
            order.StatusHistory
                .OrderBy(h => h.ChangedAt)
                .ThenBy(h => h.Id)
                .Select(h => new OrderStatusHistoryDto(h.Status, h.ChangedAt, h.Note))
                .ToList());
    }

    public static OrderTrackingDto ToTrackingDto(this Order order)
    {
        return new OrderTrackingDto(
            order.OrderNumber,
            order.Status,
            order.OrderType,
            order.Total,
            order.CreatedAt,
            order.UpdatedAt,
            order.StatusHistory
                .OrderBy(h => h.ChangedAt)
                .ThenBy(h => h.Id)
                .Select(h => new OrderStatusHistoryDto(h.Status, h.ChangedAt, h.Note))
                .ToList(),
            order.Items
                .OrderBy(i => i.Id)
                .Select(i => new OrderItemDto(
                    i.Id,
                    i.MenuItemId,
                    i.VariantId,
                    i.ItemNameAr,
                    i.VariantNameAr,
                    i.UnitPrice,
                    i.Quantity,
                    i.LineTotal,
                    i.Notes))
                .ToList());
    }

    private static T DeserializeOrDefault<T>(string json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
