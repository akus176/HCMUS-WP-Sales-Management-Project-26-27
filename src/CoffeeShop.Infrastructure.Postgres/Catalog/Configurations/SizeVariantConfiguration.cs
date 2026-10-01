namespace CoffeeShop.Infrastructure.Postgres.Catalog.Configurations;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class SizeVariantConfiguration : IEntityTypeConfiguration<SizeVariant>
{
    public void Configure(EntityTypeBuilder<SizeVariant> builder)
    {
        builder.ToTable("size_variants", "catalog", table =>
        {
            table.HasCheckConstraint("CK_size_variants_price", "price >= 0");
        });
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("id");
        builder.Property(v => v.MenuItemId)
            .HasConversion<EntityIdValueConverter>()
            .IsRequired()
            .HasColumnName("menu_item_id");
        builder.Property(v => v.SizeName)
            .HasMaxLength(40)
            .IsRequired()
            .HasColumnName("size_name");
        builder.Property(v => v.Price)
            .HasConversion<MoneyValueConverter>()
            .HasPrecision(12, 2)
            .IsRequired()
            .HasColumnName("price");
        builder.Property(v => v.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.HasIndex(v => new { v.MenuItemId, v.SizeName }).IsUnique();
        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(v => v.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
