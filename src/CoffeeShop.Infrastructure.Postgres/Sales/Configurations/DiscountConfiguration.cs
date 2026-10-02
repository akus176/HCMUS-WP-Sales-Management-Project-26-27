namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.ToTable("discounts", "sales", table =>
        {
            table.HasCheckConstraint("CK_discounts_type", "type IN ('fixed', 'percentage')");
            table.HasCheckConstraint("CK_discounts_value", "value > 0");
            table.HasCheckConstraint("CK_discounts_status", "status IN ('pending', 'approved', 'rejected')");
            table.HasCheckConstraint("CK_discounts_computed_amount", "computed_amount IS NULL OR computed_amount >= 0");
        });
        builder.HasKey(discount => discount.Id);
        builder.Property(discount => discount.Id).HasConversion<EntityIdValueConverter>().HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(discount => discount.OrderId).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("order_id");
        builder.Property(discount => discount.Type).HasMaxLength(10).IsRequired().HasColumnName("type");
        builder.Property(discount => discount.Value).HasPrecision(12, 2).IsRequired().HasColumnName("value");
        builder.Property(discount => discount.Status).HasMaxLength(10).IsRequired().HasDefaultValue("pending").HasColumnName("status");
        builder.Property(discount => discount.RequestedBy).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("requested_by");
        builder.Property(discount => discount.RequestedAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()").HasColumnName("requested_at");
        builder.Property(discount => discount.ApprovedBy).HasConversion<EntityIdValueConverter>().HasColumnName("approved_by");
        builder.Property(discount => discount.ApprovedAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").HasColumnName("approved_at");
        builder.Property(discount => discount.ComputedAmount).HasConversion<MoneyValueConverter>().HasPrecision(12, 2).HasColumnName("computed_amount");
        builder.HasIndex(discount => new { discount.OrderId, discount.Status });
        builder.HasOne<Order>().WithMany().HasForeignKey(discount => discount.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(discount => discount.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(discount => discount.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
