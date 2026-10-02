namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OrderLineAddonConfiguration : IEntityTypeConfiguration<OrderLineAddon>
{
    public void Configure(EntityTypeBuilder<OrderLineAddon> builder)
    {
        builder.ToTable("order_line_addons", "sales", table => table.HasCheckConstraint("CK_order_line_addons_price_snapshot", "addon_price_snapshot >= 0"));
        builder.HasKey(addon => addon.Id);
        builder.Property(addon => addon.Id).HasConversion<EntityIdValueConverter>().HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(addon => addon.OrderLineId).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("order_line_id");
        builder.Property(addon => addon.AddonId).HasConversion<EntityIdValueConverter>().HasColumnName("addon_id");
        builder.Property(addon => addon.AddonNameSnapshot).HasMaxLength(80).IsRequired().HasColumnName("addon_name_snapshot");
        builder.Property(addon => addon.AddonPriceSnapshot).HasConversion<MoneyValueConverter>().HasPrecision(12, 2).IsRequired().HasColumnName("addon_price_snapshot");
        builder.HasIndex(addon => addon.OrderLineId);
        builder.HasOne<OrderLine>().WithMany().HasForeignKey(addon => addon.OrderLineId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Addon>().WithMany().HasForeignKey(addon => addon.AddonId).OnDelete(DeleteBehavior.Restrict);
    }
}
