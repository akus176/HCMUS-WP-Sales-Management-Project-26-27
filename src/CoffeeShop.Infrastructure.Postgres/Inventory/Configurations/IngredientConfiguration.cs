namespace CoffeeShop.Infrastructure.Postgres.Inventory.Configurations;

using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.ToTable("ingredients", "inventory", table =>
        {
            table.HasCheckConstraint("CK_ingredients_available_quantity", "available_quantity >= 0");
            table.HasCheckConstraint("CK_ingredients_reorder_threshold", "reorder_threshold >= 0");
        });
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("id");
        builder.Property(i => i.Name)
            .HasMaxLength(100)
            .IsRequired()
            .HasColumnName("name");
        builder.Property(i => i.BaseUnit)
            .HasMaxLength(20)
            .IsRequired()
            .HasColumnName("base_unit");
        builder.Property(i => i.AvailableQuantity)
            .HasPrecision(14, 3)
            .IsRequired()
            .HasDefaultValue(0m)
            .HasColumnName("available_quantity");
        builder.Property(i => i.ReorderThreshold)
            .HasPrecision(14, 3)
            .IsRequired()
            .HasDefaultValue(0m)
            .HasColumnName("reorder_threshold");
        builder.Property(i => i.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");
        builder.Property(i => i.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasDefaultValueSql("now()")
            .HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasDefaultValueSql("now()")
            .HasColumnName("updated_at");

        builder.HasIndex(i => i.Name).IsUnique();
        builder.HasIndex(i => i.IsActive);
    }
}
