namespace CoffeeShop.Infrastructure.Postgres.Catalog.Configurations;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MenuItemAddonConfiguration : IEntityTypeConfiguration<MenuItemAddon>
{
    public void Configure(EntityTypeBuilder<MenuItemAddon> builder)
    {
        builder.ToTable("menu_item_addons", "catalog");
        builder.HasKey(a => new { a.MenuItemId, a.AddonId });

        builder.Property(a => a.MenuItemId)
            .HasConversion<EntityIdValueConverter>()
            .ValueGeneratedNever()
            .IsRequired()
            .HasColumnName("menu_item_id");
        builder.Property(a => a.AddonId)
            .HasConversion<EntityIdValueConverter>()
            .ValueGeneratedNever()
            .IsRequired()
            .HasColumnName("addon_id");

        builder.HasIndex(a => a.AddonId);
        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(a => a.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Addon>()
            .WithMany()
            .HasForeignKey(a => a.AddonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
