using System.Text.Json;
using BabAlHara.Api.Dtos;
using BabAlHara.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BabAlHara.Api.Data;

public static class DbSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedAsync(
        AppDbContext db,
        IConfiguration config,
        string contentRootPath,
        CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        if (!await db.AdminUsers.AnyAsync(ct))
        {
            var username = config["Admin:Username"] ?? "admin";
            var password = config["Admin:DefaultPassword"] ?? "BabAlHara@2026";
            var displayName = config["Admin:DisplayName"] ?? "إدارة باب الحارة";

            var hasher = new PasswordHasher<AdminUser>();
            var admin = new AdminUser
            {
                Username = username.Trim(),
                DisplayName = displayName,
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            admin.PasswordHash = hasher.HashPassword(admin, password);
            db.AdminUsers.Add(admin);
            await db.SaveChangesAsync(ct);
        }

        var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "seed-data.json");
        if (!File.Exists(seedPath))
        {
            seedPath = Path.Combine(contentRootPath, "Data", "seed-data.json");
        }

        if (!File.Exists(seedPath))
        {
            throw new FileNotFoundException("ملف البيانات الأساسية غير موجود.", seedPath);
        }

        var json = await File.ReadAllTextAsync(seedPath, ct);
        var seed = JsonSerializer.Deserialize<SeedRoot>(json, JsonOptions)
            ?? throw new InvalidOperationException("تعذر قراءة ملف البيانات الأساسية.");

        if (!await db.RestaurantSettings.AnyAsync(ct) && seed.Restaurant is not null)
        {
            var r = seed.Restaurant;
            db.RestaurantSettings.Add(new RestaurantSetting
            {
                NameAr = r.NameAr,
                NameEn = r.NameEn,
                TaglineAr = r.TaglineAr,
                DescriptionAr = r.DescriptionAr,
                AddressAr = r.AddressAr,
                AddressEn = r.AddressEn,
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                PlusCode = r.PlusCode,
                GoogleMapsUrl = r.GoogleMapsUrl,
                PhonesJson = JsonSerializer.Serialize(r.Phones ?? [], JsonOptions),
                WhatsappNumber = r.WhatsappNumber,
                ComplaintsPhone = r.ComplaintsPhone,
                FacebookUrl = r.FacebookUrl,
                DeliveryFee = r.DeliveryFee,
                MinOrderAmount = 0m,
                AcceptingOrders = true,
                PaymentMethodsJson = JsonSerializer.Serialize(r.PaymentMethods ?? [], JsonOptions),
                ServicesJson = JsonSerializer.Serialize(r.Services ?? [], JsonOptions),
                OpeningHoursJson = JsonSerializer.Serialize(r.OpeningHours ?? [], JsonOptions),
                RatingsJson = JsonSerializer.Serialize(r.Ratings ?? new RatingsDto(new GoogleRatingDto(0, 0), new FacebookRatingDto(0, 0)), JsonOptions),
                LogoUrl = r.LogoUrl,
                CoverUrl = r.CoverUrl,
                MenuImageUrl = r.MenuImageUrl
            });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Offers.AnyAsync(ct) && seed.Offers is not null)
        {
            var sortOrder = 1;
            foreach (var o in seed.Offers)
            {
                db.Offers.Add(new Offer
                {
                    TitleAr = o.TitleAr,
                    DescriptionAr = o.DescriptionAr,
                    ImageUrl = o.ImageUrl,
                    IsActive = o.IsActive,
                    SortOrder = sortOrder++
                });
            }
            await db.SaveChangesAsync(ct);
        }

        if (!await db.MenuCategories.AnyAsync(ct) && seed.Categories is not null)
        {
            foreach (var c in seed.Categories)
            {
                var cat = new MenuCategory
                {
                    Slug = c.Slug,
                    NameAr = c.NameAr,
                    NameEn = c.NameEn,
                    ImageUrl = c.ImageUrl,
                    SortOrder = c.SortOrder
                };

                var itemOrder = 1;
                foreach (var i in c.Items ?? [])
                {
                    var item = new MenuItem
                    {
                        NameAr = i.NameAr,
                        NameEn = i.NameEn,
                        DescriptionAr = i.DescriptionAr,
                        ImageUrl = i.ImageUrl,
                        IsAvailable = i.IsAvailable ?? true,
                        IsFeatured = i.IsFeatured ?? false,
                        SortOrder = itemOrder++,
                        LastKnownPrice2020 = i.LastKnownPrice2020
                    };

                    var varOrder = 1;
                    foreach (var v in i.Variants ?? [])
                    {
                        item.Variants.Add(new MenuItemVariant
                        {
                            NameAr = v.NameAr,
                            Price = v.Price,
                            SortOrder = varOrder++
                        });
                    }

                    cat.Items.Add(item);
                }

                db.MenuCategories.Add(cat);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private sealed record SeedRoot(
        SeedRestaurant? Restaurant,
        List<SeedOffer>? Offers,
        List<SeedCategory>? Categories);

    private sealed record SeedRestaurant(
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
        List<string>? Phones,
        string WhatsappNumber,
        string? ComplaintsPhone,
        string FacebookUrl,
        decimal DeliveryFee,
        List<string>? PaymentMethods,
        List<string>? Services,
        List<OpeningHourDto>? OpeningHours,
        RatingsDto? Ratings,
        string LogoUrl,
        string CoverUrl,
        string MenuImageUrl);

    private sealed record SeedOffer(
        string TitleAr,
        string DescriptionAr,
        string? ImageUrl,
        bool IsActive);

    private sealed record SeedCategory(
        string Slug,
        string NameAr,
        string? NameEn,
        string? ImageUrl,
        int SortOrder,
        List<SeedItem>? Items);

    private sealed record SeedItem(
        string NameAr,
        string? NameEn,
        string? DescriptionAr,
        string? ImageUrl,
        bool? IsAvailable,
        bool? IsFeatured,
        decimal? LastKnownPrice2020,
        List<SeedVariant>? Variants);

    private sealed record SeedVariant(string? NameAr, decimal? Price);
}
