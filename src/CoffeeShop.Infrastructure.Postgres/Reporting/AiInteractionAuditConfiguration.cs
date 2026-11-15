namespace CoffeeShop.Infrastructure.Postgres.Reporting;

using CoffeeShop.Domain.Reporting;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AiInteractionAuditConfiguration : IEntityTypeConfiguration<AiInteractionAudit>
{
    public void Configure(EntityTypeBuilder<AiInteractionAudit> builder)
    {
        builder.ToTable("ai_interaction_audits", "reporting");
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Id).HasConversion<EntityIdValueConverter>().HasColumnName("id");
        builder.Property(audit => audit.UserId).HasConversion<EntityIdValueConverter>().HasColumnName("user_id");
        builder.Property(audit => audit.OccurredAt).HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz").HasColumnName("occurred_at");
        builder.Property(audit => audit.Question).HasMaxLength(2000).HasColumnName("question");
        builder.Property(audit => audit.Tools).HasMaxLength(200).HasColumnName("tools");
        builder.Property(audit => audit.Outcome).HasMaxLength(40).HasColumnName("outcome");
        builder.Property(audit => audit.DurationMs).HasColumnName("duration_ms");
        builder.HasIndex(audit => new { audit.UserId, audit.OccurredAt });
        builder.HasOne<CoffeeShop.Domain.Identity.User>().WithMany().HasForeignKey(audit => audit.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
