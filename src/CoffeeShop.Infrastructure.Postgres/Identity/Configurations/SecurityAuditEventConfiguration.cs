namespace CoffeeShop.Infrastructure.Postgres.Identity.Configurations;

using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("security_audit_events", "identity");

        builder.HasKey(sae => sae.Id);

        builder.Property(sae => sae.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(sae => sae.EventType)
            .HasMaxLength(40)
            .IsRequired()
            .HasColumnName("event_type");

        builder.Property(sae => sae.UserId)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("user_id");

        builder.Property(sae => sae.OccurredAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("occurred_at");

        builder.Property(sae => sae.Outcome)
            .HasMaxLength(20)
            .IsRequired()
            .HasColumnName("outcome");

        builder.Property(sae => sae.IpAddress)
            .HasMaxLength(45)
            .HasColumnName("ip_address");

        builder.Property(sae => sae.Metadata)
            .HasColumnType("jsonb")
            .HasColumnName("metadata");

        builder.HasIndex(sae => new { sae.UserId, sae.OccurredAt });
        builder.HasIndex(sae => new { sae.EventType, sae.OccurredAt });

        builder.HasOne(sae => sae.User)
            .WithMany()
            .HasForeignKey(sae => sae.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
