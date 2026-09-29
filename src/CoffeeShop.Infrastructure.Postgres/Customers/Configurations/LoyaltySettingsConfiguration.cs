namespace CoffeeShop.Infrastructure.Postgres.Customers.Configurations;

using CoffeeShop.Domain.Customers;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class LoyaltySettingsConfiguration : IEntityTypeConfiguration<LoyaltySettings>
{
    public void Configure(EntityTypeBuilder<LoyaltySettings> builder)
    {
        builder.ToTable("loyalty_settings", "customers", table =>
        {
            table.HasCheckConstraint("CK_loyalty_settings_max_redeemable_percentage", "max_redeemable_percentage >= 0 AND max_redeemable_percentage <= 100");
        });

        builder.HasKey(ls => ls.Id);

        builder.Property(ls => ls.Id)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("id");

        builder.Property(ls => ls.EarnPointsPerCurrencyUnit)
            .HasPrecision(8, 4)
            .IsRequired()
            .HasColumnName("earn_points_per_currency_unit");

        builder.Property(ls => ls.RedeemValuePerPoint)
            .HasPrecision(8, 4)
            .IsRequired()
            .HasColumnName("redeem_value_per_point");

        builder.Property(ls => ls.MaxRedeemablePercentage)
            .HasPrecision(5, 2)
            .IsRequired()
            .HasColumnName("max_redeemable_percentage");

        builder.Property(ls => ls.UpdatedAt)
            .HasConversion<UtcTimestampValueConverter>()
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasColumnName("updated_at");

        builder.Property(ls => ls.UpdatedBy)
            .HasConversion<EntityIdValueConverter>()
            .HasColumnName("updated_by");
    }
}
