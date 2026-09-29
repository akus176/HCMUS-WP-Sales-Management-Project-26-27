namespace CoffeeShop.Infrastructure.Postgres.Identity.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "identity");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(u => u.Username)
            .HasMaxLength(50)
            .IsRequired()
            .HasColumnName("username");

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.Email)
            .HasMaxLength(255)
            .IsRequired()
            .HasColumnName("email");

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255)
            .IsRequired()
            .HasColumnName("password_hash");

        builder.Property(u => u.FullName)
            .HasMaxLength(150)
            .IsRequired()
            .HasColumnName("full_name");

        builder.Property(u => u.Phone)
            .HasMaxLength(30)
            .HasColumnName("phone");

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.Property(u => u.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(u => u.UpdatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("updated_at");

        builder.Property(u => u.DeactivatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .HasColumnName("deactivated_at");

        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
