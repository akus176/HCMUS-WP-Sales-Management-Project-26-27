namespace CoffeeShop.Domain.Customers;

using CoffeeShop.Domain.Common;

public class LoyaltySettings
{
    // Parameterless constructor for EF Core
    protected LoyaltySettings()
    {
    }

    public LoyaltySettings(
        EntityId id,
        decimal earnPointsPerCurrencyUnit,
        decimal redeemValuePerPoint,
        decimal maxRedeemablePercentage,
        UtcTimestamp updatedAt,
        EntityId? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        Id = id;
        Update(earnPointsPerCurrencyUnit, redeemValuePerPoint, maxRedeemablePercentage, updatedAt, updatedBy);
    }

    public void Update(
        decimal earnPointsPerCurrencyUnit,
        decimal redeemValuePerPoint,
        decimal maxRedeemablePercentage,
        UtcTimestamp updatedAt,
        EntityId? updatedBy = null)
    {
        ValidateRate(earnPointsPerCurrencyUnit, nameof(earnPointsPerCurrencyUnit));
        ValidateRate(redeemValuePerPoint, nameof(redeemValuePerPoint));

        if (maxRedeemablePercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRedeemablePercentage), "Max redeemable percentage must be between 0 and 100.");
        }

        if (decimal.Round(maxRedeemablePercentage, 2) != maxRedeemablePercentage)
        {
            throw new ArgumentException("Maximum redemption percentage supports at most two decimal places.", nameof(maxRedeemablePercentage));
        }

        ArgumentNullException.ThrowIfNull(updatedAt);

        EarnPointsPerCurrencyUnit = earnPointsPerCurrencyUnit;
        RedeemValuePerPoint = redeemValuePerPoint;
        MaxRedeemablePercentage = maxRedeemablePercentage;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    private static void ValidateRate(decimal rate, string parameterName)
    {
        // Match numeric(8,4), avoiding silent rounding when settings are persisted.
        if (rate is < 0 or > 9999.9999m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Loyalty rates must be between 0 and 9999.9999.");
        }

        if (decimal.Round(rate, 4) != rate)
        {
            throw new ArgumentException("Loyalty rates support at most four decimal places.", parameterName);
        }
    }

    public EntityId Id { get; protected set; } = null!;

    public decimal EarnPointsPerCurrencyUnit { get; protected set; }

    public decimal RedeemValuePerPoint { get; protected set; }

    public decimal MaxRedeemablePercentage { get; protected set; }

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;

    public EntityId? UpdatedBy { get; protected set; }
}
