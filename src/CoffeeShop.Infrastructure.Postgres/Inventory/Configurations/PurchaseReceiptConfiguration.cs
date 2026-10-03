namespace CoffeeShop.Infrastructure.Postgres.Inventory.Configurations;

using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("purchase_receipts", "inventory", table =>
        {
            table.HasCheckConstraint("CK_purchase_receipts_status", "status IN (0, 1, 2)");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(r => r.SupplierId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("supplier_id");

        builder.Property(r => r.ReceiptNumber)
            .HasMaxLength(50)
            .IsRequired()
            .HasColumnName("receipt_number");

        builder.HasIndex(r => r.ReceiptNumber)
            .IsUnique();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(r => r.PostedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .HasColumnName("posted_at");

        builder.Property(r => r.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(r => r.UpdatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("updated_at");

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(r => r.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Lines)
            .WithOne(l => l.PurchaseReceipt)
            .HasForeignKey(l => l.PurchaseReceiptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
