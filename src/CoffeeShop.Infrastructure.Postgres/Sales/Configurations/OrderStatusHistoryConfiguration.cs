namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("order_status_history", "sales", table => table.HasCheckConstraint("CK_order_status_history_to_status", "to_status IN ('draft', 'confirmed', 'preparing', 'ready', 'completed', 'cancelled')"));
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).HasConversion<EntityIdValueConverter>().HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(history => history.OrderId).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("order_id");
        builder.Property(history => history.FromStatus).HasMaxLength(15).HasColumnName("from_status");
        builder.Property(history => history.ToStatus).HasMaxLength(15).IsRequired().HasColumnName("to_status");
        builder.Property(history => history.ChangedBy).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("changed_by");
        builder.Property(history => history.ChangedAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()").HasColumnName("changed_at");
        builder.HasIndex(history => new { history.OrderId, history.ChangedAt });
        builder.HasOne<Order>().WithMany(order => order.StatusHistory).HasForeignKey(history => history.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(history => history.ChangedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
