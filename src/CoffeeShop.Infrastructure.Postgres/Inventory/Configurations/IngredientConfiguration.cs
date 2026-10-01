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
            table.HasCheckConstraint("CK_ingredients_current_balance", "current_balance >= 0");
            table.HasCheckConstraint("CK_ingredients_minimum_level", "minimum_level >= 0");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(i => i.Name)
            .HasMaxLength(120)
            .IsRequired()
            .HasColumnName("name");

        builder.HasIndex(i => i.Name)
            .IsUnique();

        builder.Property(i => i.BaseUnit)
            .HasMaxLength(20)
            .IsRequired()
            .HasColumnName("base_unit");

        builder.Property(i => i.CurrentBalance)
            .HasColumnType("numeric(14,3)")
            .IsRequired()
            .HasColumnName("current_balance");

        builder.Property(i => i.MinimumLevel)
            .HasColumnType("numeric(14,3)")
            .IsRequired()
            .HasColumnName("minimum_level");

        builder.Property(i => i.UnitCost)
            .HasConversion<MoneyValueConverter>()
            .HasColumnType("numeric(12,2)")
            .IsRequired()
            .HasColumnName("unit_cost");

        builder.Property(i => i.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.Property(i => i.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(i => i.UpdatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("updated_at");
    }
}