namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", "sales", table =>
        {
            table.HasCheckConstraint("CK_payments_method", "method IN ('cash', 'card', 'e_wallet')");
            table.HasCheckConstraint("CK_payments_amount", "amount > 0");
            table.HasCheckConstraint("CK_payments_status", "status IN ('pending', 'succeeded', 'failed')");
            table.HasCheckConstraint(
                "CK_payments_required_reference",
                "method = 'cash' OR (reference IS NOT NULL AND btrim(reference) <> '')");
        });
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).HasConversion<EntityIdValueConverter>().HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(payment => payment.OrderId).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("order_id");
        builder.Property(payment => payment.Method).HasMaxLength(10).IsRequired().HasColumnName("method");
        builder.Property(payment => payment.Amount).HasConversion<MoneyValueConverter>().HasPrecision(12, 2).IsRequired().HasColumnName("amount");
        builder.Property(payment => payment.Status).HasMaxLength(10).IsRequired().HasDefaultValue("succeeded").HasColumnName("status");
        builder.Property(payment => payment.Reference).HasMaxLength(100).HasColumnName("reference");
        builder.Property(payment => payment.PaidAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()").HasColumnName("paid_at");
        builder.Property(payment => payment.RecordedBy).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("recorded_by");
        builder.HasIndex(payment => payment.OrderId);
        builder.HasOne<Order>().WithMany(order => order.Payments).HasForeignKey(payment => payment.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(payment => payment.RecordedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
