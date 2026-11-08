namespace CoffeeShop.Infrastructure.Postgres.Identity.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MobileDeviceRegistrationConfiguration : IEntityTypeConfiguration<MobileDeviceRegistration>
{
    public void Configure(EntityTypeBuilder<MobileDeviceRegistration> builder)
    {
        builder.ToTable("mobile_device_registrations", "identity");
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).HasConversion<EntityIdValueConverter>().HasColumnName("id");
        builder.Property(device => device.UserId).HasConversion<EntityIdValueConverter>().HasColumnName("user_id").IsRequired();
        builder.Property(device => device.Platform).HasColumnName("platform").HasMaxLength(32).IsRequired();
        builder.Property(device => device.Token).HasColumnName("token").HasMaxLength(512).IsRequired();
        builder.Property(device => device.LastSeenAt).HasConversion<UtcTimestampValueConverter>().HasColumnName("last_seen_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(device => device.RevokedAt).HasConversion<UtcTimestampValueConverter>().HasColumnName("revoked_at").HasColumnType("timestamptz");
        builder.HasIndex(device => device.Token).IsUnique().HasFilter("revoked_at IS NULL");
        builder.HasIndex(device => device.UserId);
        builder.HasOne(device => device.User).WithMany().HasForeignKey(device => device.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
