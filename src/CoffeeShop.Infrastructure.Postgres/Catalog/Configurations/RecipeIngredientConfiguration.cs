namespace CoffeeShop.Infrastructure.Postgres.Catalog.Configurations;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("recipe_ingredients", "catalog", table =>
        {
            table.HasCheckConstraint("CK_recipe_ingredients_quantity", "quantity > 0");
        });
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("id");
        builder.Property(r => r.SizeVariantId)
            .HasConversion<EntityIdValueConverter>()
            .IsRequired()
            .HasColumnName("size_variant_id");
        builder.Property(r => r.IngredientId)
            .HasConversion<EntityIdValueConverter>()
            .IsRequired()
            .HasColumnName("ingredient_id");
        builder.Property(r => r.Quantity)
            .HasPrecision(12, 3)
            .IsRequired()
            .HasColumnName("quantity");
        builder.Property(r => r.Unit)
            .HasMaxLength(20)
            .IsRequired()
            .HasColumnName("unit");

        builder.HasIndex(r => new { r.SizeVariantId, r.IngredientId }).IsUnique();
        builder.HasIndex(r => r.IngredientId);
        builder.HasOne<SizeVariant>()
            .WithMany()
            .HasForeignKey(r => r.SizeVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ingredient>()
            .WithMany()
            .HasForeignKey(r => r.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
