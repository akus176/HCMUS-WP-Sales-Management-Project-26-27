namespace CoffeeShop.Infrastructure.Postgres.Customers.Configurations;

using CoffeeShop.Domain.Customers;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("customer_addresses", "customers");

        builder.HasKey(ca => ca.Id);

        builder.Property(ca => ca.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(ca => ca.CustomerId)
            .HasConversion<EntityIdValueConverter>()
            .IsRequired()
            .HasColumnName("customer_id");

        builder.Property(ca => ca.Label)
            .HasMaxLength(40)
            .HasColumnName("label");

        builder.Property(ca => ca.AddressLine)
            .HasColumnType("text")
            .IsRequired()
            .HasColumnName("address_line");

        builder.Property(ca => ca.IsDefault)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_default");

        builder.Property(ca => ca.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.HasIndex(ca => ca.CustomerId);

        builder.HasOne(ca => ca.Customer)
            .WithMany(c => c.Addresses)
            .HasForeignKey(ca => ca.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
