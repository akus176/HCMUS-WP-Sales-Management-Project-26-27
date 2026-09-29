namespace CoffeeShop.Infrastructure.Postgres.Identity.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", "identity");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(r => r.Code)
            .HasMaxLength(30)
            .IsRequired()
            .HasColumnName("code");

        builder.HasIndex(r => r.Code)
            .IsUnique();

        builder.Property(r => r.Name)
            .HasMaxLength(50)
            .IsRequired()
            .HasColumnName("name");

        builder.Property(r => r.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        builder.HasMany(r => r.UserRoles)
            .WithOne(ur => ur.Role)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.RolePermissions)
            .WithOne(rp => rp.Role)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
