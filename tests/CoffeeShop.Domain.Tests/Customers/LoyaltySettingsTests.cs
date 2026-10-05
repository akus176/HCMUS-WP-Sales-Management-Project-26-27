namespace CoffeeShop.Domain.Tests.Customers;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

public class LoyaltySettingsTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));

    public static TheoryData<decimal, decimal, decimal> InvalidSettings => new()
    {
        { -0.0001m, 1m, 50m },
        { 10000m, 1m, 50m },
        { 0.00001m, 1m, 50m },
        { 1m, -0.0001m, 50m },
        { 1m, 10000m, 50m },
        { 1m, 0.00001m, 50m },
        { 1m, 1m, -0.01m },
        { 1m, 1m, 100.01m },
        { 1m, 1m, 33.333m },
        { decimal.MaxValue, 1m, 50m },
    };

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void CreateAndUpdate_RejectInvalidSettingsWithoutMutation(decimal earnRate, decimal redeemRate, decimal percentage)
    {
        var actor = EntityId.New();
        var settings = new LoyaltySettings(EntityId.New(), 1m, 0.5m, 20m, Now, actor);
        var changed = new UtcTimestamp(Now.Value.AddHours(1));

        Assert.ThrowsAny<ArgumentException>(() => new LoyaltySettings(EntityId.New(), earnRate, redeemRate, percentage, Now));
        Assert.ThrowsAny<ArgumentException>(() => settings.Update(earnRate, redeemRate, percentage, changed, EntityId.New()));

        Assert.Equal(1m, settings.EarnPointsPerCurrencyUnit);
        Assert.Equal(0.5m, settings.RedeemValuePerPoint);
        Assert.Equal(20m, settings.MaxRedeemablePercentage);
        Assert.Equal(Now, settings.UpdatedAt);
        Assert.Equal(actor, settings.UpdatedBy);
    }

    [Fact]
    public void Update_AcceptsStorageBoundariesAndPreservesIdentity()
    {
        var id = EntityId.New();
        var actor = EntityId.New();
        var settings = new LoyaltySettings(id, 0m, 0m, 0m, Now);
        var changed = new UtcTimestamp(Now.Value.AddHours(1));

        settings.Update(9999.9999m, 9999.9999m, 100m, changed, actor);

        Assert.Equal(id, settings.Id);
        Assert.Equal(9999.9999m, settings.EarnPointsPerCurrencyUnit);
        Assert.Equal(9999.9999m, settings.RedeemValuePerPoint);
        Assert.Equal(100m, settings.MaxRedeemablePercentage);
        Assert.Equal(changed, settings.UpdatedAt);
        Assert.Equal(actor, settings.UpdatedBy);
    }

    [Fact]
    public void Update_MissingTimestampLeavesSettingsUnchanged()
    {
        var settings = new LoyaltySettings(EntityId.New(), 1m, 0.5m, 20m, Now);

        Assert.Throws<ArgumentNullException>(() => settings.Update(2m, 1m, 30m, null!));
        Assert.Equal(1m, settings.EarnPointsPerCurrencyUnit);
        Assert.Equal(0.5m, settings.RedeemValuePerPoint);
        Assert.Equal(20m, settings.MaxRedeemablePercentage);
        Assert.Equal(Now, settings.UpdatedAt);
    }
}
