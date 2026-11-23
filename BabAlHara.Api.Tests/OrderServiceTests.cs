using BabAlHara.Api.Data;
using BabAlHara.Api.Dtos;
using BabAlHara.Api.Entities;
using BabAlHara.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BabAlHara.Api.Tests;

public class OrderServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly FakeNotifier _notifier = new();
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        SeedTestCatalog();
        _service = new OrderService(_db, _notifier);
    }

    private void SeedTestCatalog()
    {
        _db.RestaurantSettings.Add(new RestaurantSetting
        {
            NameAr = "باب الحارة",
            NameEn = "Bab Al Hara",
            DeliveryFee = 10m,
            MinOrderAmount = 0m,
            AcceptingOrders = true
        });

        var boxes = new MenuCategory
        {
            Slug = "boxes",
            NameAr = "بوكسات وميكسات",
            SortOrder = 1
        };

        boxes.Items.Add(new MenuItem
        {
            NameAr = "بوكس الشبيح",
            IsAvailable = true,
            SortOrder = 1,
            Variants =
            [
                new MenuItemVariant { NameAr = "ورك", Price = 150m, SortOrder = 1 },
                new MenuItemVariant { NameAr = "صدر", Price = 165m, SortOrder = 2 }
            ]
        });

        boxes.Items.Add(new MenuItem
        {
            NameAr = "فرخة فحم كاملة",
            IsAvailable = false,
            SortOrder = 2,
            Variants =
            [
                new MenuItemVariant { NameAr = null, Price = null, SortOrder = 1 }
            ]
        });

        boxes.Items.Add(new MenuItem
        {
            NameAr = "صنف متاح بدون سعر",
            IsAvailable = true,
            SortOrder = 3,
            Variants =
            [
                new MenuItemVariant { NameAr = null, Price = null, SortOrder = 1 }
            ]
        });

        _db.MenuCategories.Add(boxes);
        _db.SaveChanges();
    }

    [Fact]
    public async Task CreateOrder_ComputesServerTotalsAndSequentialNumbers()
    {
        var shabih = await _db.MenuItems.Include(i => i.Variants).FirstAsync(i => i.NameAr == "بوكس الشبيح");
        var sadrVariant = shabih.Variants.First(v => v.NameAr == "صدر");

        var deliveryReq = new CreateOrderRequest(
            "أحمد محمود",
            "٠١٠١٧٨٥٩٥٧٥",
            OrderType.Delivery,
            "سنورس شارع بورسعيد بجوار بنك القاهرة",
            "بدون شطة",
            OrderChannel.Website,
            [new CreateOrderItemRequest(shabih.Id, sadrVariant.Id, 2, null)]);

        var order1 = await _service.CreateOrderAsync(deliveryReq, isAdminOrder: false);

        Assert.Equal("BH-000001", order1.OrderNumber);
        Assert.Equal("01017859575", order1.CustomerPhone);
        Assert.Equal(330m, order1.Subtotal);
        Assert.Equal(10m, order1.DeliveryFee);
        Assert.Equal(340m, order1.Total);
        Assert.Single(_notifier.CreatedOrders);

        var pickupReq = new CreateOrderRequest(
            "سارة علي",
            "01100019756",
            OrderType.Pickup,
            null,
            null,
            OrderChannel.WhatsApp,
            [new CreateOrderItemRequest(shabih.Id, sadrVariant.Id, 1, null)]);

        var order2 = await _service.CreateOrderAsync(pickupReq, isAdminOrder: false);

        Assert.Equal("BH-000002", order2.OrderNumber);
        Assert.Equal(165m, order2.Subtotal);
        Assert.Equal(0m, order2.DeliveryFee);
        Assert.Equal(165m, order2.Total);
        Assert.Equal(2, _notifier.CreatedOrders.Count);
    }

    [Fact]
    public async Task CreateOrder_RejectsUnavailableOrNullPricedItems()
    {
        var unavailable = await _db.MenuItems.Include(i => i.Variants).FirstAsync(i => i.NameAr == "فرخة فحم كاملة");
        var unpriced = await _db.MenuItems.Include(i => i.Variants).FirstAsync(i => i.NameAr == "صنف متاح بدون سعر");

        var req1 = new CreateOrderRequest(
            "عميل تجربة",
            "01017859575",
            OrderType.Pickup,
            null,
            null,
            OrderChannel.Website,
            [new CreateOrderItemRequest(unavailable.Id, unavailable.Variants.First().Id, 1, null)]);

        await Assert.ThrowsAsync<DomainValidationException>(() => _service.CreateOrderAsync(req1, isAdminOrder: false));

        var req2 = new CreateOrderRequest(
            "عميل تجربة",
            "01017859575",
            OrderType.Pickup,
            null,
            null,
            OrderChannel.Website,
            [new CreateOrderItemRequest(unpriced.Id, unpriced.Variants.First().Id, 1, null)]);

        await Assert.ThrowsAsync<DomainValidationException>(() => _service.CreateOrderAsync(req2, isAdminOrder: false));
    }

    [Fact]
    public async Task CreateOrder_BlocksPublicWhenKillSwitchOff_AllowsAdminPhoneOrder()
    {
        var settings = await _db.RestaurantSettings.FirstAsync();
        settings.AcceptingOrders = false;
        await _db.SaveChangesAsync();

        var shabih = await _db.MenuItems.Include(i => i.Variants).FirstAsync(i => i.NameAr == "بوكس الشبيح");
        var variantId = shabih.Variants.First().Id;

        var publicReq = new CreateOrderRequest(
            "عميل أونلاين",
            "01017859575",
            OrderType.Pickup,
            null,
            null,
            OrderChannel.Website,
            [new CreateOrderItemRequest(shabih.Id, variantId, 1, null)]);

        await Assert.ThrowsAsync<DomainConflictException>(() => _service.CreateOrderAsync(publicReq, isAdminOrder: false));

        var phoneReq = new CreateOrderRequest(
            "عميل تليفون",
            "01017859575",
            OrderType.Delivery,
            "سنورس بجوار الشؤون",
            null,
            OrderChannel.Phone,
            [new CreateOrderItemRequest(shabih.Id, variantId, 1, null)]);

        var created = await _service.CreateOrderAsync(phoneReq, isAdminOrder: true);
        Assert.Equal("BH-000001", created.OrderNumber);
        Assert.Equal(OrderChannel.Phone, created.Channel);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class FakeNotifier : IOrderNotifier
    {
        public List<OrderDto> CreatedOrders { get; } = [];
        public List<OrderDto> UpdatedOrders { get; } = [];

        public Task NotifyOrderCreatedAsync(OrderDto order, CancellationToken ct = default)
        {
            CreatedOrders.Add(order);
            return Task.CompletedTask;
        }

        public Task NotifyOrderUpdatedAsync(OrderDto order, CancellationToken ct = default)
        {
            UpdatedOrders.Add(order);
            return Task.CompletedTask;
        }
    }
}
