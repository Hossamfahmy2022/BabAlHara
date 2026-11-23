using System.Text.RegularExpressions;
using BabAlHara.Api.Data;
using BabAlHara.Api.Dtos;
using BabAlHara.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BabAlHara.Api.Services;

public partial class OrderService(
    AppDbContext db,
    IOrderNotifier notifier)
{
    [GeneratedRegex(@"^01[0125]\d{8}$")]
    private static partial Regex EgyptianMobileRegex();

    public static string NormalizeDigits(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var chars = input.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                >= '٠' and <= '٩' => (char)('0' + (chars[i] - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (chars[i] - '۰')),
                _ => chars[i]
            };
        }

        return new string(chars).Replace(" ", string.Empty).Replace("-", string.Empty);
    }

    public async Task<OrderDto> CreateOrderAsync(
        CreateOrderRequest request,
        bool isAdminOrder,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, List<string>>();
        void AddError(string key, string msg)
        {
            if (!errors.TryGetValue(key, out var list))
            {
                list = [];
                errors[key] = list;
            }
            list.Add(msg);
        }

        var customerName = request.CustomerName?.Trim() ?? string.Empty;
        if (customerName.Length is < 2 or > 80)
        {
            AddError("customerName", "يجب أن يكون الاسم بين 2 و 80 حرفاً.");
        }

        var customerPhone = NormalizeDigits(request.CustomerPhone);
        if (!EgyptianMobileRegex().IsMatch(customerPhone))
        {
            AddError("customerPhone", "يرجى إدخال رقم موبايل مصري صحيح (مثال: 01012345678).");
        }

        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        if (request.OrderType == OrderType.Delivery)
        {
            if (string.IsNullOrWhiteSpace(address) || address.Length is < 5 or > 300)
            {
                AddError("address", "يرجى كتابة عنوان التوصيل بالتفصيل (من 5 إلى 300 حرف).");
            }
        }
        else if (address is { Length: > 300 })
        {
            AddError("address", "العنوان طويل جداً (الحد الأقصى 300 حرف).");
        }

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (notes is { Length: > 500 })
        {
            AddError("notes", "الملاحظات طويلة جداً (الحد الأقصى 500 حرف).");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            AddError("items", "يجب اختيار صنف واحد على الأقل.");
        }
        else
        {
            for (var idx = 0; idx < request.Items.Count; idx++)
            {
                var line = request.Items[idx];
                if (line.Quantity is < 1 or > 50)
                {
                    AddError("items", "الكمية لكل صنف يجب أن تكون بين 1 و 50.");
                    break;
                }
                if (line.Notes?.Trim().Length > 200)
                {
                    AddError("items", "ملاحظات الصنف يجب ألا تتجاوز 200 حرف.");
                    break;
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(
                errors.ToDictionary(k => k.Key, v => v.Value.ToArray()));
        }

        var settings = await db.RestaurantSettings.FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("إعدادات المطعم غير مهيأة.");

        if (!isAdminOrder && !settings.AcceptingOrders)
        {
            throw new DomainConflictException("المطعم لا يستقبل طلبات أونلاين حالياً، يرجى الاتصال بنا أو المحاولة لاحقاً.");
        }

        var itemIds = request.Items!.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await db.MenuItems
            .Include(m => m.Variants)
            .Where(m => itemIds.Contains(m.Id) && !m.IsDeleted)
            .ToDictionaryAsync(m => m.Id, ct);

        var orderItems = new List<OrderItem>();
        decimal subtotal = 0m;

        foreach (var reqItem in request.Items!)
        {
            if (!menuItems.TryGetValue(reqItem.MenuItemId, out var menuItem))
            {
                throw new DomainValidationException("items", "أحد الأصناف المختارة غير موجود.");
            }

            if (!menuItem.IsAvailable)
            {
                throw new DomainValidationException("items", $"الصنف \"{menuItem.NameAr}\" غير متاح حالياً.");
            }

            var variant = menuItem.Variants.FirstOrDefault(v => v.Id == reqItem.VariantId && !v.IsDeleted);
            if (variant is null)
            {
                throw new DomainValidationException("items", $"الحجم أو الخيار المحدد للصنف \"{menuItem.NameAr}\" غير متاح.");
            }

            if (variant.Price is null or <= 0)
            {
                throw new DomainValidationException("items", $"الصنف \"{menuItem.NameAr}\" غير مسعر حالياً.");
            }

            var unitPrice = variant.Price.Value;
            var lineTotal = unitPrice * reqItem.Quantity;
            subtotal += lineTotal;

            orderItems.Add(new OrderItem
            {
                MenuItemId = menuItem.Id,
                VariantId = variant.Id,
                ItemNameAr = menuItem.NameAr,
                VariantNameAr = variant.NameAr,
                UnitPrice = unitPrice,
                Quantity = reqItem.Quantity,
                LineTotal = lineTotal,
                Notes = string.IsNullOrWhiteSpace(reqItem.Notes) ? null : reqItem.Notes.Trim()
            });
        }

        if (!isAdminOrder && settings.MinOrderAmount > 0 && subtotal < settings.MinOrderAmount)
        {
            throw new DomainValidationException("items", $"الحد الأدنى للطلب هو {settings.MinOrderAmount:0.##} ج.م.");
        }

        var deliveryFee = request.OrderType == OrderType.Delivery ? settings.DeliveryFee : 0m;
        var total = subtotal + deliveryFee;
        var now = DateTime.UtcNow;

        var order = new Order
        {
            OrderNumber = $"TMP-{Guid.NewGuid():N}"[..20],
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            OrderType = request.OrderType,
            Address = request.OrderType == OrderType.Delivery ? address : null,
            Notes = notes,
            Channel = request.Channel,
            Status = OrderStatus.New,
            Subtotal = subtotal,
            DeliveryFee = deliveryFee,
            Total = total,
            CreatedAt = now,
            UpdatedAt = now,
            Items = orderItems,
            StatusHistory =
            [
                new OrderStatusHistory
                {
                    Status = OrderStatus.New,
                    ChangedAt = now,
                    Note = isAdminOrder ? "تم تسجيل الطلب بواسطة الإدارة" : "تم استلام الطلب"
                }
            ]
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        order.OrderNumber = $"BH-{order.Id:D6}";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var dto = order.ToDto();
        await notifier.NotifyOrderCreatedAsync(dto, ct);
        return dto;
    }

    public async Task<OrderDto> UpdateStatusAsync(
        int orderId,
        OrderStatus status,
        string? note,
        CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new KeyNotFoundException("الطلب غير موجود.");

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > 300 })
        {
            throw new DomainValidationException("note", "الملاحظة طويلة جداً (الحد الأقصى 300 حرف).");
        }

        var now = DateTime.UtcNow;
        order.Status = status;
        order.UpdatedAt = now;
        order.StatusHistory.Add(new OrderStatusHistory
        {
            Status = status,
            ChangedAt = now,
            Note = trimmedNote
        });

        await db.SaveChangesAsync(ct);

        var dto = order.ToDto();
        await notifier.NotifyOrderUpdatedAsync(dto, ct);
        return dto;
    }
}
