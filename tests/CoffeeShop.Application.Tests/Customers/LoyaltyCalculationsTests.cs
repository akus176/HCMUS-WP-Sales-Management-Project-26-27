namespace CoffeeShop.Application.Tests.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;

public class LoyaltyCalculationsTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void SettingsChanges_AffectSubsequentCalculations()
    {
        var result = LoyaltyCalculations.CreateSettings(EntityId.New(), 0.1m, 0.5m, 20m, Now);
        Assert.True(result.IsSuccess);
        var settings = result.Value;
        var customer = CreateCustomer();
        var draft = new StoredOrder(customer.Id);
        var completed = new StoredOrder(customer.Id, OrderStatus.Completed, 100m);

        Assert.Equal(10, LoyaltyCalculations.CalculateEarnedPoints(customer, completed, settings).Value);
        Assert.Equal(40, LoyaltyCalculations.CalculateMaximumRedeemablePoints(customer, draft, settings).Value);
        Assert.Equal(new Money(10m), LoyaltyCalculations.CalculateRedemptionDiscount(customer, draft, settings, 20).Value);

        var actor = EntityId.New();
        var changed = new UtcTimestamp(Now.Value.AddHours(1));
        Assert.True(LoyaltyCalculations.UpdateSettings(settings, 0.2m, 1m, 30m, changed, actor).IsSuccess);

        Assert.Equal(20, LoyaltyCalculations.CalculateEarnedPoints(customer, completed, settings).Value);
        Assert.Equal(30, LoyaltyCalculations.CalculateMaximumRedeemablePoints(customer, draft, settings).Value);
        Assert.Equal(new Money(20m), LoyaltyCalculations.CalculateRedemptionDiscount(customer, draft, settings, 20).Value);
        Assert.Equal(changed, settings.UpdatedAt);
        Assert.Equal(actor, settings.UpdatedBy);
        Assert.Equal(100, customer.LoyaltyPointsBalance);
        Assert.Empty(customer.LoyaltyTransactions);
    }

    [Fact]
    public void InvalidSettings_ReturnValidationWithoutChangingConfiguration()
    {
        var invalidCreation = LoyaltyCalculations.CreateSettings(EntityId.New(), -1m, 1m, 20m, Now);
        AssertValidation(invalidCreation, "earnPointsPerCurrencyUnit");
        var settings = new LoyaltySettings(EntityId.New(), 1m, 1m, 20m, Now);

        var update = LoyaltyCalculations.UpdateSettings(settings, 2m, 0.00001m, 30m, Now);

        AssertValidation(update, "redeemValuePerPoint");
        Assert.Equal(1m, settings.EarnPointsPerCurrencyUnit);
        Assert.Equal(1m, settings.RedeemValuePerPoint);
        Assert.Equal(20m, settings.MaxRedeemablePercentage);
    }

    [Fact]
    public void InvalidRedemption_ReturnsValidationWithPointsTarget()
    {
        var customer = CreateCustomer();
        var result = LoyaltyCalculations.CalculateRedemptionDiscount(customer, new StoredOrder(customer.Id), Settings(), 21);

        AssertValidation(result, "points");
        Assert.Equal(100, customer.LoyaltyPointsBalance);
    }

    [Fact]
    public void InactiveCustomer_ReturnsConflictForEarningAndRedemption()
    {
        var customer = CreateCustomer();
        customer.Deactivate(Now);

        AssertConflict(LoyaltyCalculations.CalculateEarnedPoints(customer, new StoredOrder(customer.Id, OrderStatus.Completed, 100m), Settings()));
        AssertConflict(LoyaltyCalculations.CalculateMaximumRedeemablePoints(customer, new StoredOrder(customer.Id), Settings()));
        AssertConflict(LoyaltyCalculations.CalculateRedemptionDiscount(customer, new StoredOrder(customer.Id), Settings(), 10));
    }

    [Fact]
    public void IneligibleOrder_ReturnsConflict()
    {
        var customer = CreateCustomer();
        AssertConflict(LoyaltyCalculations.CalculateEarnedPoints(customer, new StoredOrder(customer.Id), Settings()));
        AssertConflict(LoyaltyCalculations.CalculateEarnedPoints(customer, new StoredOrder(customer.Id, OrderStatus.Completed, 99m), Settings()));
        AssertConflict(LoyaltyCalculations.CalculateRedemptionDiscount(customer, new StoredOrder(customer.Id, OrderStatus.Completed, 100m), Settings(), 10));
    }

    [Fact]
    public void MissingOrMismatchedInputs_ReturnValidation()
    {
        var customer = CreateCustomer();
        var order = new StoredOrder(customer.Id);

        AssertValidation(LoyaltyCalculations.CalculateRedemptionDiscount(null!, order, Settings(), 1), "customer");
        AssertValidation(LoyaltyCalculations.CalculateRedemptionDiscount(customer, null!, Settings(), 1), "order");
        AssertValidation(LoyaltyCalculations.CalculateRedemptionDiscount(customer, order, null!, 1), "settings");
        AssertValidation(LoyaltyCalculations.CalculateRedemptionDiscount(customer, new StoredOrder(EntityId.New()), Settings(), 1), "customer");
    }

    [Fact]
    public void BalanceOverflow_ReturnsValidationWithoutMutation()
    {
        var customer = new Customer(EntityId.New(), "Alice", "0901234567", Now, Now, loyaltyPointsBalance: int.MaxValue);

        AssertValidation(LoyaltyCalculations.CalculateEarnedPoints(customer, new StoredOrder(customer.Id, OrderStatus.Completed, 100m), Settings()), "order");
        Assert.Equal(int.MaxValue, customer.LoyaltyPointsBalance);
    }

    private static void AssertValidation(Result result, string target)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("loyalty.invalid_input", result.Error.Code);
        Assert.Equal(target, Assert.Single(result.Error.Details).Target);
    }

    private static void AssertConflict(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("loyalty.invalid_state", result.Error.Code);
    }

    private static Customer CreateCustomer() => new(EntityId.New(), "Alice", "0901234567", Now, Now, loyaltyPointsBalance: 100);

    private static LoyaltySettings Settings() => new(EntityId.New(), 1m, 1m, 20m, Now);

    private sealed class StoredOrder : Order
    {
        public StoredOrder(EntityId customerId, string status = OrderStatus.Draft, decimal paid = 0m)
        {
            CustomerId = customerId;
            Total = new Money(100m);
            PaidAmount = new Money(paid);
            Status = status;
        }
    }
}
