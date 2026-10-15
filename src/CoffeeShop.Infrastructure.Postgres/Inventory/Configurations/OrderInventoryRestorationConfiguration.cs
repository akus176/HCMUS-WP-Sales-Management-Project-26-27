namespace CoffeeShop.Infrastructure.Postgres.Inventory.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OrderInventoryRestorationConfiguration : IEntityTypeConfiguration<OrderInventoryRestoration>
{
    public void Configure(EntityTypeBuilder<OrderInventoryRestoration> builder)
    {
        builder.ToTable("order_inventory_restorations", "inventory");
        builder.HasKey(restoration => restoration.OrderId);
        builder.Property(restoration => restoration.OrderId).HasConversion<EntityIdValueConverter>().HasColumnName("order_id");
        builder.Property(restoration => restoration.RestoredAt).HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz").IsRequired().HasColumnName("restored_at");
        builder.Property(restoration => restoration.RestoredBy).HasConversion<EntityIdValueConverter>()
            .IsRequired().HasColumnName("restored_by");
        builder.HasOne<Order>().WithMany().HasForeignKey(restoration => restoration.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(restoration => restoration.RestoredBy).OnDelete(DeleteBehavior.Restrict);
    }
}
