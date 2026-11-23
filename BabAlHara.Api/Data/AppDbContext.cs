using BabAlHara.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BabAlHara.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RestaurantSetting> RestaurantSettings => Set<RestaurantSetting>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuItemVariant> MenuItemVariants => Set<MenuItemVariant>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RestaurantSetting>(entity =>
        {
            entity.Property(e => e.DeliveryFee).HasPrecision(10, 2);
            entity.Property(e => e.MinOrderAmount).HasPrecision(10, 2);
        });

        modelBuilder.Entity<MenuCategory>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Slug).HasMaxLength(80);
            entity.Property(e => e.NameAr).HasMaxLength(120);
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Items)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.NameAr).HasMaxLength(160);
            entity.Property(e => e.LastKnownPrice2020).HasPrecision(10, 2);
            entity.HasIndex(e => new { e.CategoryId, e.IsDeleted, e.SortOrder });
        });

        modelBuilder.Entity<MenuItemVariant>(entity =>
        {
            entity.HasOne(e => e.MenuItem)
                  .WithMany(m => m.Variants)
                  .HasForeignKey(e => e.MenuItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CustomerPhone);

            entity.Property(e => e.OrderNumber).HasMaxLength(20);
            entity.Property(e => e.CustomerName).HasMaxLength(80);
            entity.Property(e => e.CustomerPhone).HasMaxLength(20);
            entity.Property(e => e.OrderType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Channel).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Subtotal).HasPrecision(10, 2);
            entity.Property(e => e.DeliveryFee).HasPrecision(10, 2);
            entity.Property(e => e.Total).HasPrecision(10, 2);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
            entity.Property(e => e.LineTotal).HasPrecision(10, 2);
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.StatusHistory)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(60);
        });
    }
}
