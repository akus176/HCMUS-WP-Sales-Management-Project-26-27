namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("deliveries", "sales", table => table.HasCheckConstraint("CK_deliveries_status", "status IN ('unassigned', 'assigned', 'out_for_delivery', 'delivered', 'failed')"));
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).HasConversion<EntityIdValueConverter>().HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(delivery => delivery.OrderId).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("order_id");
        builder.Property(delivery => delivery.RecipientName).HasMaxLength(120).IsRequired().HasColumnName("recipient_name");
        builder.Property(delivery => delivery.ContactNumber).HasMaxLength(30).IsRequired().HasColumnName("contact_number");
        builder.Property(delivery => delivery.AddressLine).HasColumnType("text").IsRequired().HasColumnName("address_line");
        builder.Property(delivery => delivery.CustomerAddressId).HasConversion<EntityIdValueConverter>().HasColumnName("customer_address_id");
        builder.Property(delivery => delivery.Status).HasMaxLength(16).IsRequired().HasDefaultValue("unassigned").HasColumnName("status");
        builder.Property(delivery => delivery.AssignedStaffId).HasConversion<EntityIdValueConverter>().HasColumnName("assigned_staff_id");
        builder.Property(delivery => delivery.AssignedAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").HasColumnName("assigned_at");
        builder.Property(delivery => delivery.OutForDeliveryAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").HasColumnName("out_for_delivery_at");
        builder.Property(delivery => delivery.DeliveredAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").HasColumnName("delivered_at");
        builder.Property(delivery => delivery.FailedAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").HasColumnName("failed_at");
        builder.Property(delivery => delivery.FailureReason).HasColumnType("text").HasColumnName("failure_reason");
        builder.HasIndex(delivery => delivery.OrderId).IsUnique();
        builder.HasIndex(delivery => delivery.Status);
        builder.HasIndex(delivery => delivery.AssignedStaffId);
        builder.HasOne<Order>().WithMany().HasForeignKey(delivery => delivery.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CustomerAddress>().WithMany().HasForeignKey(delivery => delivery.CustomerAddressId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(delivery => delivery.AssignedStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
