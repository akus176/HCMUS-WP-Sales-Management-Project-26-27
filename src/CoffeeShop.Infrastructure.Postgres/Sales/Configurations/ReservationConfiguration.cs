namespace CoffeeShop.Infrastructure.Postgres.Sales.Configurations;

using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations", "sales", table =>
        {
            table.HasCheckConstraint("CK_reservations_party_size", "party_size > 0");
            table.HasCheckConstraint("CK_reservations_status", "status IN ('pending', 'confirmed', 'seated', 'cancelled', 'completed')");
        });
        builder.HasKey(reservation => reservation.Id);
        builder.Property(reservation => reservation.Id).HasConversion<EntityIdValueConverter>().HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(reservation => reservation.TableId).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("table_id");
        builder.Property(reservation => reservation.CustomerId).HasConversion<EntityIdValueConverter>().HasColumnName("customer_id");
        builder.Property(reservation => reservation.GuestName).HasMaxLength(120).IsRequired().HasColumnName("guest_name");
        builder.Property(reservation => reservation.ContactNumber).HasMaxLength(30).IsRequired().HasColumnName("contact_number");
        builder.Property(reservation => reservation.PartySize).IsRequired().HasColumnName("party_size");
        builder.Property(reservation => reservation.ReservedFor).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").IsRequired().HasColumnName("reserved_for");
        builder.Property(reservation => reservation.Status).HasMaxLength(15).IsRequired().HasDefaultValue("pending").HasColumnName("status");
        builder.Property(reservation => reservation.CreatedBy).HasConversion<EntityIdValueConverter>().IsRequired().HasColumnName("created_by");
        builder.Property(reservation => reservation.CreatedAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").IsRequired().HasDefaultValueSql("now()").HasColumnName("created_at");
        builder.Property(reservation => reservation.CancelledAt).HasConversion<UtcTimestampValueConverter>().HasColumnType("timestamptz").HasColumnName("cancelled_at");
        builder.HasIndex(reservation => new { reservation.TableId, reservation.ReservedFor });
        builder.HasIndex(reservation => new { reservation.Status, reservation.ReservedFor });
        builder.HasOne<DiningTable>().WithMany().HasForeignKey(reservation => reservation.TableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(reservation => reservation.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(reservation => reservation.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
