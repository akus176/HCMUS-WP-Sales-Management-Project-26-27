namespace CoffeeShop.Infrastructure.Postgres.Inventory.Configurations;

using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", "inventory", table =>
        {
            table.HasCheckConstraint("CK_stock_movements_type", "type IN (0, 1, 2, 3)");
            table.HasCheckConstraint("CK_stock_movements_quantity_delta", "quantity_delta <> 0");
        });

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(m => m.IngredientId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("ingredient_id");

        builder.Property(m => m.Type)
            .IsRequired()
            .HasColumnName("type");

        builder.Property(m => m.QuantityDelta)
            .HasPrecision(14, 3)
            .IsRequired()
            .HasColumnName("quantity_delta");

        builder.Property(m => m.ActorId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("actor_id");

        builder.Property(m => m.Reason)
            .HasMaxLength(255)
            .IsRequired()
            .HasColumnName("reason");

        builder.Property(m => m.OccurredAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("occurred_at");

        builder.Property(m => m.PurchaseReceiptId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("purchase_receipt_id");

        builder.Property(m => m.OrderId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("order_id");

        builder.HasOne<Ingredient>()
            .WithMany()
            .HasForeignKey(m => m.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.IngredientId, m.OccurredAt });

        builder.HasIndex(m => m.OrderId)
            .HasFilter("order_id IS NOT NULL");

        builder.HasIndex(m => m.PurchaseReceiptId)
            .HasFilter("purchase_receipt_id IS NOT NULL");
    }
}