namespace CoffeeShop.Application.Tests.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

public class LoyaltyLedgerTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void EarnAndRedeem_ReturnRecordedEntries()
    {
        var customer = CreateCustomer();
        var order = EntityId.New();
        var earn = LoyaltyLedger.Earn(customer, EntityId.New(), 20, Now, order);
        var redeem = LoyaltyLedger.Redeem(customer, EntityId.New(), 10, Now, order);

        Assert.True(earn.IsSuccess);
        Assert.True(redeem.IsSuccess);
        Assert.Equal(10, customer.LoyaltyPointsBalance);
        Assert.Equal(order, earn.Value.OrderId);
        Assert.Equal(new[] { earn.Value, redeem.Value }, customer.LoyaltyTransactions);
    }

    [Fact]
    public void InvalidInputs_ReturnValidationWithTarget()
    {
        var customer = CreateCustomer();
        AssertValidation(LoyaltyLedger.Earn(null!, EntityId.New(), 1, Now), "customer");
        AssertValidation(LoyaltyLedger.Earn(customer, null!, 1, Now), "transactionId");
        AssertValidation(LoyaltyLedger.Earn(customer, EntityId.New(), 0, Now), "points");
        AssertValidation(LoyaltyLedger.Redeem(customer, EntityId.New(), -1, Now), "points");
        Assert.Empty(customer.LoyaltyTransactions);
    }

    [Fact]
    public void InsufficientBalanceAndInactiveCustomer_ReturnConflictWithoutMutation()
    {
        var customer = CreateCustomer();
        AssertConflict(LoyaltyLedger.Redeem(customer, EntityId.New(), 1, Now));
        customer.Deactivate(Now);
        AssertConflict(LoyaltyLedger.Earn(customer, EntityId.New(), 1, Now));
        Assert.Equal(0, customer.LoyaltyPointsBalance);
        Assert.Empty(customer.LoyaltyTransactions);
    }

    [Fact]
    public void ReverseAndRetry_ReturnSameEntryEvenIfSettingsChanged()
    {
        var customer = CreateCustomer();
        var settings = new LoyaltySettings(EntityId.New(), 1m, 1m, 20m, Now);
        var original = LoyaltyLedger.Earn(customer, EntityId.New(), 20, Now).Value;
        settings.Update(2m, 2m, 30m, Now);
        var reversal = LoyaltyLedger.Reverse(customer, EntityId.New(), original.Id, Now);
        var retry = LoyaltyLedger.Reverse(customer, EntityId.New(), original.Id, Now);

        Assert.True(reversal.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Same(reversal.Value, retry.Value);
        Assert.Equal(-20, reversal.Value.Points);
        Assert.Equal(0, customer.LoyaltyPointsBalance);
        Assert.Equal(2, customer.LoyaltyTransactions.Count);
    }

    [Fact]
    public void OverdrawnReversal_ReturnsConflictWithoutChangingHistory()
    {
        var customer = CreateCustomer();
        var original = LoyaltyLedger.Earn(customer, EntityId.New(), 20, Now).Value;
        LoyaltyLedger.Redeem(customer, EntityId.New(), 15, Now);
        var later = new UtcTimestamp(Now.Value.AddHours(1));

        AssertConflict(LoyaltyLedger.Reverse(customer, EntityId.New(), original.Id, later));
        Assert.Equal(5, customer.LoyaltyPointsBalance);
        Assert.Equal(2, customer.LoyaltyTransactions.Count);
        Assert.Equal(Now, customer.UpdatedAt);
    }

    [Fact]
    public void MissingOriginal_ReturnsNotFound()
    {
        var customer = CreateCustomer();
        var result = LoyaltyLedger.Reverse(customer, EntityId.New(), EntityId.New(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("loyalty.transaction_not_found", result.Error.Code);
        Assert.Empty(customer.LoyaltyTransactions);
    }

    [Fact]
    public void Reverse_RejectsMissingArguments()
    {
        var customer = CreateCustomer();
        AssertValidation(LoyaltyLedger.Reverse(null!, EntityId.New(), EntityId.New(), Now), "customer");
        AssertValidation(LoyaltyLedger.Reverse(customer, null!, EntityId.New(), Now), "transactionId");
        AssertValidation(LoyaltyLedger.Reverse(customer, EntityId.New(), null!, Now), "originalTransactionId");
        AssertValidation(LoyaltyLedger.Reverse(customer, EntityId.New(), EntityId.New(), null!), "changedAt");
    }

    private static void AssertValidation(Result result, string target)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("loyalty.invalid_input", result.Error.Code);
        Assert.Equal(target, Assert.Single(result.Error.Details).Target);
    }

    private static void AssertConflict(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("loyalty.invalid_state", result.Error.Code);
    }

    private static Customer CreateCustomer() => new(EntityId.New(), "Alice", "0901234567", Now, Now);
}
