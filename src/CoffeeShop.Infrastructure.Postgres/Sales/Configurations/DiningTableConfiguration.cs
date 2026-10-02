namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> builder)
    {
        builder.ToTable("tables", "sales");
        builder.HasKey(table => table.Id);
        builder.Property(table => table.Id).HasConversion<EntityIdValueConverter>().HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(table => table.TableNumber).HasMaxLength(20).IsRequired().HasColumnName("table_number");
        builder.Property(table => table.Seats).HasColumnName("seats");
        builder.Property(table => table.IsOccupied).IsRequired().HasDefaultValue(false).HasColumnName("is_occupied");
        builder.Property(table => table.ActiveOrderId).HasConversion<EntityIdValueConverter>().HasColumnName("active_order_id");
        builder.Property(table => table.Version).IsRowVersion().HasColumnName("xmin");

        builder.HasIndex(table => table.TableNumber).IsUnique();
        builder.HasIndex(table => table.ActiveOrderId).IsUnique().HasFilter("active_order_id IS NOT NULL");
        builder.HasOne<Order>().WithOne().HasForeignKey<DiningTable>(table => table.ActiveOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
