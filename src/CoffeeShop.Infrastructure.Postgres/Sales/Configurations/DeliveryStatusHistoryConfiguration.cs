namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class DeliveryStatusHistoryConfiguration : IEntityTypeConfiguration<DeliveryStatusHistory>
{
    public void Configure(EntityTypeBuilder<DeliveryStatusHistory> builder)
    {
        builder.ToTable("delivery_status_history", "sales", table => table.HasCheckConstraint("CK_delivery_status_history_status", "from_status IN ('unassigned', 'assigned', 'out_for_delivery', 'delivered', 'failed') AND to_status IN ('unassigned', 'assigned', 'out_for_delivery', 'delivered', 'failed')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion<EntityIdValueConverter>().HasColumnName("id");
        builder.Property(x => x.DeliveryId).HasConversion<EntityIdValueConverter>().HasColumnName("delivery_id");
        builder.Property(x => x.FromStatus).HasMaxLength(16).HasColumnName("from_status");
        builder.Property(x => x.ToStatus).HasMaxLength(16).HasColumnName("to_status");
        builder.Property(x => x.ActorId).HasConversion<EntityIdValueConverter>().HasColumnName("actor_id");
        builder.Property(x => x.ChangedAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").HasColumnName("changed_at");
        builder.HasIndex(x => new { x.DeliveryId, x.ChangedAt });
        builder.HasOne<Delivery>().WithMany(x => x.StatusHistory).HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
