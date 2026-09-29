namespace CoffeeShop.Infrastructure.Postgres.Identity.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", "identity");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(rt => rt.UserId)
            .HasConversion<EntityIdValueConverter>()
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(rt => rt.TokenHash)
            .HasMaxLength(255)
            .IsRequired()
            .HasColumnName("token_hash");

        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique();

        builder.Property(rt => rt.FamilyId)
            .IsRequired()
            .HasColumnName("family_id");

        builder.Property(rt => rt.CreatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(rt => rt.ExpiresAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("expires_at");

        builder.Property(rt => rt.RevokedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .HasColumnName("revoked_at");

        builder.Property(rt => rt.ReplacedById)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("replaced_by_id");

        builder.Property(rt => rt.CreatedByIp)
            .HasMaxLength(45)
            .HasColumnName("created_by_ip");

        builder.Ignore(rt => rt.IsActive);

        builder.HasIndex(rt => rt.UserId);
        builder.HasIndex(rt => rt.FamilyId);

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rt => rt.ReplacedBy)
            .WithMany()
            .HasForeignKey(rt => rt.ReplacedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
