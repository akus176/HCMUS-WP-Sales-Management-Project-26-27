namespace CoffeeShop.Infrastructure.Postgres.Catalog.Configurations;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AddonConfiguration : IEntityTypeConfiguration<Addon>
{
    public void Configure(EntityTypeBuilder<Addon> builder)
    {
        builder.ToTable("addons", "catalog", table =>
        {
            table.HasCheckConstraint("CK_addons_price", "price >= 0");
        });
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("id");
        builder.Property(a => a.Name)
            .HasMaxLength(80)
            .IsRequired()
            .HasColumnName("name");
        builder.Property(a => a.Price)
            .HasConversion<MoneyValueConverter>()
            .HasPrecision(12, 2)
            .IsRequired()
            .HasColumnName("price");
        builder.Property(a => a.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");
    }
}
