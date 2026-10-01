namespace CoffeeShop.Infrastructure.Postgres.Catalog.Configurations;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", "catalog");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("id");
        builder.Property(c => c.Name)
            .HasMaxLength(80)
            .IsRequired()
            .HasColumnName("name");
        builder.Property(c => c.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("display_order");
        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");
        builder.Property(c => c.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");
    }
}
