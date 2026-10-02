namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "sales", table =>
        {
            table.HasCheckConstraint("CK_orders_channel", "channel IN ('dine_in', 'takeaway', 'delivery')");
            table.HasCheckConstraint("CK_orders_status", "status IN ('draft', 'confirmed', 'preparing', 'ready', 'completed', 'cancelled')");
            table.HasCheckConstraint("CK_orders_discount_total", "discount_total >= 0");
            table.HasCheckConstraint("CK_orders_total", "total >= 0");
            table.HasCheckConstraint("CK_orders_paid_amount", "paid_amount >= 0");
            table.HasCheckConstraint("CK_orders_loyalty_points_redeemed", "loyalty_points_redeemed >= 0");
            table.HasCheckConstraint("CK_orders_loyalty_discount_amount", "loyalty_discount_amount >= 0");
        });
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).HasConversion<EntityIdValueConverter>().HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(order => order.OrderNumber).HasMaxLength(30).IsRequired().HasColumnName("order_number");
        builder.Property(order => order.Channel).HasMaxLength(10).IsRequired().HasColumnName("channel");
        builder.Property(order => order.Status).HasMaxLength(15).IsRequired().HasDefaultValue("draft").HasColumnName("status");
        builder.Property(order => order.CustomerId).HasConversion<EntityIdValueConverter>().HasColumnName("customer_id");
        builder.Property(order => order.TableId).HasConversion<EntityIdValueConverter>().HasColumnName("table_id");
        ConfigureMoney(builder.Property(order => order.Subtotal), "subtotal", defaultValue: true);
        ConfigureMoney(builder.Property(order => order.DiscountTotal), "discount_total", defaultValue: true);
        ConfigureMoney(builder.Property(order => order.Total), "total", defaultValue: true);
        ConfigureMoney(builder.Property(order => order.PaidAmount), "paid_amount", defaultValue: true);
        builder.Property(order => order.LoyaltyPointsRedeemed).IsRequired().HasDefaultValue(0).HasColumnName("loyalty_points_redeemed");
        ConfigureMoney(builder.Property(order => order.LoyaltyDiscountAmount), "loyalty_discount_amount", defaultValue: true);
        builder.Property(order => order.CreatedBy).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("created_by");
        ConfigureTimestamp(builder.Property(order => order.CreatedAt), "created_at", true);
        ConfigureTimestamp(builder.Property(order => order.ConfirmedAt), "confirmed_at", false);
        ConfigureTimestamp(builder.Property(order => order.CompletedAt), "completed_at", false);
        ConfigureTimestamp(builder.Property(order => order.CancelledAt), "cancelled_at", false);
        builder.Property(order => order.CancelReason).HasColumnType("text").HasColumnName("cancel_reason");
        builder.Property(order => order.Version).IsRowVersion().HasColumnName("xmin");

        builder.HasIndex(order => order.OrderNumber).IsUnique();
        builder.HasIndex(order => order.Status);
        builder.HasIndex(order => order.CustomerId);
        builder.HasIndex(order => new { order.Channel, order.CreatedAt });
        builder.HasIndex(order => order.TableId);
        builder.HasOne<Customer>().WithMany().HasForeignKey(order => order.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DiningTable>().WithMany().HasForeignKey(order => order.TableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(order => order.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureMoney(PropertyBuilder<CoffeeShop.Domain.Common.Money> property, string columnName, bool defaultValue = false)
    {
        property.HasConversion<MoneyValueConverter>().HasPrecision(12, 2).IsRequired().HasColumnName(columnName);
        if (defaultValue)
        {
            property.HasDefaultValue(CoffeeShop.Domain.Common.Money.Zero);
        }
    }

    private static void ConfigureTimestamp(PropertyBuilder<CoffeeShop.Domain.Common.UtcTimestamp> property, string columnName, bool defaultValue)
    {
        property.HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").IsRequired().HasColumnName(columnName);
        if (defaultValue)
        {
            property.HasDefaultValueSql("now()");
        }
    }

    private static void ConfigureTimestamp(PropertyBuilder<CoffeeShop.Domain.Common.UtcTimestamp?> property, string columnName, bool defaultValue)
    {
        property.HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").HasColumnName(columnName);
        if (defaultValue)
        {
            property.HasDefaultValueSql("now()");
        }
    }
}
