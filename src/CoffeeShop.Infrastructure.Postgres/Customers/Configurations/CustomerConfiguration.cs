namespace CoffeeShop.Infrastructure.Postgres.Customers.Configurations;

using CoffeeShop.Domain.Customers;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers", "customers", table =>
        {
            table.HasCheckConstraint("CK_customers_loyalty_points_balance", "loyalty_points_balance >= 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(c => c.FullName)
            .HasMaxLength(120)
            .IsRequired()
            .HasColumnName("full_name");

        builder.Property(c => c.Phone)
            .HasMaxLength(30)
            .IsRequired()
            .HasColumnName("phone");

        builder.HasIndex(c => c.Phone)
            .IsUnique();

        builder.Property(c => c.Email)
            .HasMaxLength(255)
            .HasColumnName("email");

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.Property(c => c.LoyaltyPointsBalance)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("loyalty_points_balance");

        builder.Property(c => c.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(c => c.UpdatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("updated_at");

        builder.HasMany(c => c.Addresses)
            .WithOne(ca => ca.Customer)
            .HasForeignKey(ca => ca.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.LoyaltyTransactions)
            .WithOne(lt => lt.Customer)
            .HasForeignKey(lt => lt.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
