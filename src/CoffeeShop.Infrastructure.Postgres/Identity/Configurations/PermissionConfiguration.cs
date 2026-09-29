namespace CoffeeShop.Infrastructure.Postgres.Identity.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions", "identity");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(p => p.Code)
            .HasMaxLength(60)
            .IsRequired()
            .HasColumnName("code");

        builder.HasIndex(p => p.Code)
            .IsUnique();

        builder.Property(p => p.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        builder.HasMany(p => p.RolePermissions)
            .WithOne(rp => rp.Permission)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
