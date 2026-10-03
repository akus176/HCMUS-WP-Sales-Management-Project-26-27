namespace CoffeeShop.Infrastructure.Postgres.Inventory.Configurations;

using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers", "inventory");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(s => s.Name)
            .HasMaxLength(120)
            .IsRequired()
            .HasColumnName("name");

        builder.HasIndex(s => s.Name)
            .IsUnique();

        builder.Property(s => s.Phone)
            .HasMaxLength(30)
            .IsRequired()
            .HasColumnName("phone");

        builder.Property(s => s.Email)
            .HasMaxLength(255)
            .HasColumnName("email");

        builder.Property(s => s.Address)
            .HasMaxLength(255)
            .HasColumnName("address");

        builder.Property(s => s.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.Property(s => s.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(s => s.UpdatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("updated_at");
    }
}
