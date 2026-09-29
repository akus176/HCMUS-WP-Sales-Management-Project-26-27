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

        if (earnPointsPerCurrencyUnit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(earnPointsPerCurrencyUnit), "Earn points rate cannot be negative.");
        }

        if (redeemValuePerPoint < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(redeemValuePerPoint), "Redeem value per point cannot be negative.");
        }

        if (maxRedeemablePercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRedeemablePercentage), "Max redeemable percentage must be between 0 and 100.");
        }

        ArgumentNullException.ThrowIfNull(updatedAt);

        Id = id;
        EarnPointsPerCurrencyUnit = earnPointsPerCurrencyUnit;
        RedeemValuePerPoint = redeemValuePerPoint;
        MaxRedeemablePercentage = maxRedeemablePercentage;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public EntityId Id { get; protected set; } = null!;

    public decimal EarnPointsPerCurrencyUnit { get; protected set; }

    public decimal RedeemValuePerPoint { get; protected set; }

    public decimal MaxRedeemablePercentage { get; protected set; }

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;

    public EntityId? UpdatedBy { get; protected set; }
}
