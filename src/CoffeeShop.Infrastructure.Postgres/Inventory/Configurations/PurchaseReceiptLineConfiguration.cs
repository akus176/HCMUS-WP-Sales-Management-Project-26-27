namespace CoffeeShop.Infrastructure.Postgres.Inventory.Configurations;

using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PurchaseReceiptLineConfiguration : IEntityTypeConfiguration<PurchaseReceiptLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptLine> builder)
    {
        builder.ToTable("purchase_receipt_lines", "inventory", table =>
        {
            table.HasCheckConstraint("CK_purchase_receipt_lines_quantity", "quantity > 0");
        });

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(l => l.IngredientId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("ingredient_id");

        builder.Property(l => l.Quantity)
            .HasPrecision(14, 3)
            .IsRequired()
            .HasColumnName("quantity");

        builder.Property(l => l.UnitCost)
            .HasConversion<MoneyValueConverter>()
            .HasPrecision(12, 2)
            .IsRequired()
            .HasColumnName("unit_cost");

        builder.Property(l => l.PurchaseReceiptId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("purchase_receipt_id");

        builder.HasOne<Ingredient>()
            .WithMany()
            .HasForeignKey(l => l.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
