namespace CoffeeShop.Infrastructure.Postgres.Catalog.Configurations;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items", "catalog");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("id");
        builder.Property(m => m.CategoryId)
            .HasConversion<EntityIdValueConverter>()
            .IsRequired()
            .HasColumnName("category_id");
        builder.Property(m => m.Name)
            .HasMaxLength(120)
            .IsRequired()
            .HasColumnName("name");
        builder.Property(m => m.Description)
            .HasColumnType("text")
            .IsRequired(false)
            .HasColumnName("description");
        builder.Property(m => m.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");
        builder.Property(m => m.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasIndex(m => m.CategoryId);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
