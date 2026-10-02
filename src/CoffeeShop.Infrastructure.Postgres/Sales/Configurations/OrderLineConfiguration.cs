namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines", "sales", table =>
        {
            table.HasCheckConstraint("CK_order_lines_unit_price_snapshot", "unit_price_snapshot >= 0");
            table.HasCheckConstraint("CK_order_lines_quantity", "quantity > 0");
            table.HasCheckConstraint("CK_order_lines_line_total", "line_total >= 0");
        });
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).HasConversion<EntityIdValueConverter>().HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(line => line.OrderId).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("order_id");
        builder.Property(line => line.MenuItemId).HasConversion<EntityIdValueConverter>().HasColumnName("menu_item_id");
        builder.Property(line => line.SizeVariantId).HasConversion<EntityIdValueConverter>().HasColumnName("size_variant_id");
        builder.Property(line => line.ItemNameSnapshot).HasMaxLength(120).IsRequired().HasColumnName("item_name_snapshot");
        builder.Property(line => line.SizeNameSnapshot).HasMaxLength(40).IsRequired().HasColumnName("size_name_snapshot");
        builder.Property(line => line.UnitPriceSnapshot).HasConversion<MoneyValueConverter>().HasPrecision(12, 2).IsRequired().HasColumnName("unit_price_snapshot");
        builder.Property(line => line.Quantity).IsRequired().HasColumnName("quantity");
        builder.Property(line => line.LineTotal).HasConversion<MoneyValueConverter>().HasPrecision(12, 2).IsRequired().HasColumnName("line_total");
        builder.HasIndex(line => line.OrderId);
        builder.HasOne<Order>().WithMany(order => order.Lines).HasForeignKey(line => line.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MenuItem>().WithMany().HasForeignKey(line => line.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SizeVariant>().WithMany().HasForeignKey(line => line.SizeVariantId).OnDelete(DeleteBehavior.Restrict);
    }
}
