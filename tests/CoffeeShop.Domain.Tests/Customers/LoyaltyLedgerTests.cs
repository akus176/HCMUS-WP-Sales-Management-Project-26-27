namespace CoffeeShop.Domain.Tests.Customers;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

public class LoyaltyLedgerTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp Later = new(Now.Value.AddHours(1));

    [Fact]
    public void EarnAndRedeem_AppendAuditedEntriesAndPreserveEarlierBalances()
    {
        var customer = CreateCustomer();
        var orderId = EntityId.New();
        var actor = EntityId.New();
        var earned = customer.EarnLoyaltyPoints(EntityId.New(), 30, Now, orderId, actor);
        var redeemed = customer.RedeemLoyaltyPoints(EntityId.New(), 20, Later, orderId, actor);

        Assert.Equal(10, customer.LoyaltyPointsBalance);
        Assert.Equal(Later, customer.UpdatedAt);
        Assert.Equal(new[] { earned, redeemed }, customer.LoyaltyTransactions);
        Assert.Equal(30, earned.Points);
        Assert.Equal(30, earned.BalanceAfter);
        Assert.Equal(-20, redeemed.Points);
        Assert.Equal(10, redeemed.BalanceAfter);
        Assert.Equal(LoyaltyTransactionType.Earn, earned.Type);
        Assert.Equal(LoyaltyTransactionType.Redeem, redeemed.Type);
        Assert.All(customer.LoyaltyTransactions, t =>
        {
            Assert.Equal(customer.Id, t.CustomerId);
            Assert.Equal(orderId, t.OrderId);
            Assert.Equal(actor, t.CreatedBy);
            Assert.Null(t.ReversesTransactionId);
        });
        Assert.Equal(Now, earned.CreatedAt);
        Assert.Equal(Later, redeemed.CreatedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Retry_UsesOrderOrTransactionIdentityWithoutReapplying(bool hasOrder)
    {
        var customer = CreateCustomer();
        var id = EntityId.New();
        var orderId = hasOrder ? EntityId.New() : null;
        var earned = customer.EarnLoyaltyPoints(id, 30, Now, orderId);
        var retryId = hasOrder ? EntityId.New() : id;

        Assert.Same(earned, customer.EarnLoyaltyPoints(retryId, 30, Later, orderId));
        Assert.Equal(30, customer.LoyaltyPointsBalance);
        Assert.Equal(Now, customer.UpdatedAt);
        Assert.Single(customer.LoyaltyTransactions);
        Assert.Throws<InvalidOperationException>(() => customer.EarnLoyaltyPoints(retryId, 31, Later, orderId));
        Assert.Equal(30, customer.LoyaltyPointsBalance);
        Assert.Single(customer.LoyaltyTransactions);
    }

    [Fact]
    public void RedeemRetry_SucceedsEvenAfterAllAvailablePointsWereSpent()
    {
        var customer = CreateCustomer();
        customer.EarnLoyaltyPoints(EntityId.New(), 30, Now);
        var orderId = EntityId.New();
        var redeemed = customer.RedeemLoyaltyPoints(EntityId.New(), 30, Now, orderId);

        Assert.Same(redeemed, customer.RedeemLoyaltyPoints(EntityId.New(), 30, Later, orderId));
        Assert.Equal(0, customer.LoyaltyPointsBalance);
        Assert.Equal(2, customer.LoyaltyTransactions.Count);
    }

    [Fact]
    public void ReusedTransactionIdWithDifferentOperation_IsRejected()
    {
        var customer = CreateCustomer();
        var id = EntityId.New();
        customer.EarnLoyaltyPoints(id, 30, Now);

        Assert.Throws<InvalidOperationException>(() => customer.RedeemLoyaltyPoints(id, 30, Later));
        Assert.Throws<InvalidOperationException>(() => customer.EarnLoyaltyPoints(id, 30, Later, EntityId.New()));
        Assert.Equal(30, customer.LoyaltyPointsBalance);
        Assert.Single(customer.LoyaltyTransactions);
    }

    [Fact]
    public void InvalidOperations_LeaveBalanceHistoryAndTimestampUnchanged()
    {
        var customer = CreateCustomer();
        Assert.Throws<InvalidOperationException>(() => customer.RedeemLoyaltyPoints(EntityId.New(), 1, Later));
        Assert.Throws<ArgumentOutOfRangeException>(() => customer.EarnLoyaltyPoints(EntityId.New(), 0, Later));
        Assert.Throws<ArgumentOutOfRangeException>(() => customer.RedeemLoyaltyPoints(EntityId.New(), -1, Later));
        Assert.Throws<ArgumentNullException>(() => customer.EarnLoyaltyPoints(null!, 1, Later));
        Assert.Throws<ArgumentNullException>(() => customer.EarnLoyaltyPoints(EntityId.New(), 1, null!));
        Assert.Equal(0, customer.LoyaltyPointsBalance);
        Assert.Empty(customer.LoyaltyTransactions);
        Assert.Equal(Now, customer.UpdatedAt);

        customer.EarnLoyaltyPoints(EntityId.New(), int.MaxValue, Now);
        Assert.Throws<InvalidOperationException>(() => customer.EarnLoyaltyPoints(EntityId.New(), 1, Later));
        Assert.Equal(int.MaxValue, customer.LoyaltyPointsBalance);
        Assert.Single(customer.LoyaltyTransactions);
        Assert.Equal(Now, customer.UpdatedAt);
    }

    [Fact]
    public void InactiveCustomer_CannotRecordNewEarningOrRedemption()
    {
        var customer = CreateCustomer();
        customer.EarnLoyaltyPoints(EntityId.New(), 30, Now);
        customer.Deactivate(Now);

        Assert.Throws<InvalidOperationException>(() => customer.EarnLoyaltyPoints(EntityId.New(), 1, Later));
        Assert.Throws<InvalidOperationException>(() => customer.RedeemLoyaltyPoints(EntityId.New(), 1, Later));
        Assert.Equal(30, customer.LoyaltyPointsBalance);
        Assert.Single(customer.LoyaltyTransactions);
    }

    [Theory]
    [InlineData(LoyaltyTransactionType.Earn, -1)]
    [InlineData(LoyaltyTransactionType.Redeem, 1)]
    [InlineData(LoyaltyTransactionType.ReverseEarn, 1)]
    [InlineData(LoyaltyTransactionType.ReverseRedeem, -1)]
    [InlineData(LoyaltyTransactionType.Redeem, int.MinValue)]
    public void Transaction_RejectsIncorrectOrNonReversiblePoints(string type, int points)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoyaltyTransaction(
            EntityId.New(), EntityId.New(), type, points, 10, Now));
    }

    [Theory]
    [InlineData(LoyaltyTransactionType.Earn, 1, true)]
    [InlineData(LoyaltyTransactionType.Redeem, -1, true)]
    [InlineData(LoyaltyTransactionType.ReverseEarn, -1, false)]
    [InlineData(LoyaltyTransactionType.ReverseRedeem, 1, false)]
    public void Transaction_RequiresOriginalReferenceExactlyForReversals(string type, int points, bool hasOriginal)
    {
        Assert.Throws<ArgumentException>(() => new LoyaltyTransaction(
            EntityId.New(), EntityId.New(), type, points, 10, Now,
            reversesTransactionId: hasOriginal ? EntityId.New() : null));
    }

    [Fact]
    public void Transaction_CannotReverseItself()
    {
        var id = EntityId.New();
        Assert.Throws<ArgumentException>(() => new LoyaltyTransaction(
            id, EntityId.New(), LoyaltyTransactionType.ReverseEarn, -1, 10, Now, reversesTransactionId: id));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Reversal_AppendsExactOppositeAndRepeatedRequestsHaveNoEffect(bool earning, bool hasOrder)
    {
        var customer = CreateCustomer();
        customer.EarnLoyaltyPoints(EntityId.New(), 100, Now);
        var orderId = hasOrder ? EntityId.New() : null;
        var original = earning
            ? customer.EarnLoyaltyPoints(EntityId.New(), 20, Now, orderId)
            : customer.RedeemLoyaltyPoints(EntityId.New(), 20, Now, orderId);
        var actor = EntityId.New();
        customer.Deactivate(Now);
        var reversal = customer.ReverseLoyaltyTransaction(EntityId.New(), original.Id, Later, actor);

        Assert.Equal(100, customer.LoyaltyPointsBalance);
        Assert.Equal(3, customer.LoyaltyTransactions.Count);
        Assert.Equal(original.Id, reversal.ReversesTransactionId);
        Assert.Equal(customer.Id, reversal.CustomerId);
        Assert.Equal(orderId, reversal.OrderId);
        Assert.Equal(-original.Points, reversal.Points);
        Assert.Equal(100, reversal.BalanceAfter);
        Assert.Equal(earning ? LoyaltyTransactionType.ReverseEarn : LoyaltyTransactionType.ReverseRedeem, reversal.Type);
        Assert.Equal(Later, reversal.CreatedAt);
        Assert.Equal(actor, reversal.CreatedBy);
        Assert.Equal(earning ? 120 : 80, original.BalanceAfter);
        Assert.Equal(Now, original.CreatedAt);
        Assert.Null(original.ReversesTransactionId);
        Assert.Same(original, customer.LoyaltyTransactions.ElementAt(1));

        var retryTime = new UtcTimestamp(Later.Value.AddHours(1));
        Assert.Same(reversal, customer.ReverseLoyaltyTransaction(reversal.Id, original.Id, retryTime));
        Assert.Same(reversal, customer.ReverseLoyaltyTransaction(EntityId.New(), original.Id, retryTime));
        Assert.Equal(100, customer.LoyaltyPointsBalance);
        Assert.Equal(3, customer.LoyaltyTransactions.Count);
        Assert.Equal(Later, customer.UpdatedAt);
    }

    [Fact]
    public void ReversalRetry_DoesNotRevalidateBalanceAfterSubsequentSpending()
    {
        var customer = CreateCustomer();
        var original = customer.EarnLoyaltyPoints(EntityId.New(), 20, Now);
        customer.EarnLoyaltyPoints(EntityId.New(), 10, Now);
        var reversal = customer.ReverseLoyaltyTransaction(EntityId.New(), original.Id, Now);
        customer.RedeemLoyaltyPoints(EntityId.New(), 10, Now);

        Assert.Same(reversal, customer.ReverseLoyaltyTransaction(EntityId.New(), original.Id, Later));
        Assert.Equal(0, customer.LoyaltyPointsBalance);
        Assert.Equal(4, customer.LoyaltyTransactions.Count);
        Assert.Equal(Now, customer.UpdatedAt);
    }

    [Fact]
    public void SpentEarning_ReversalFailsAtomicallyAndCanBeRetriedAfterReplenishment()
    {
        var customer = CreateCustomer();
        var original = customer.EarnLoyaltyPoints(EntityId.New(), 20, Now);
        customer.RedeemLoyaltyPoints(EntityId.New(), 15, Now);
        var reversalId = EntityId.New();

        Assert.Throws<InvalidOperationException>(() => customer.ReverseLoyaltyTransaction(reversalId, original.Id, Later));
        Assert.Equal(5, customer.LoyaltyPointsBalance);
        Assert.Equal(2, customer.LoyaltyTransactions.Count);
        Assert.Equal(Now, customer.UpdatedAt);

        customer.EarnLoyaltyPoints(EntityId.New(), 15, Now);
        var reversal = customer.ReverseLoyaltyTransaction(reversalId, original.Id, Later);
        Assert.Equal(0, customer.LoyaltyPointsBalance);
        Assert.Equal(reversalId, reversal.Id);
        Assert.Equal(-20, reversal.Points);
        Assert.Equal(4, customer.LoyaltyTransactions.Count);
    }

    [Fact]
    public void RedemptionReversal_RejectsOverflowWithoutMutation()
    {
        var customer = CreateCustomer();
        customer.EarnLoyaltyPoints(EntityId.New(), int.MaxValue, Now);
        var original = customer.RedeemLoyaltyPoints(EntityId.New(), 1, Now);
        customer.EarnLoyaltyPoints(EntityId.New(), 1, Now);

        Assert.Throws<InvalidOperationException>(() => customer.ReverseLoyaltyTransaction(EntityId.New(), original.Id, Later));
        Assert.Equal(int.MaxValue, customer.LoyaltyPointsBalance);
        Assert.Equal(3, customer.LoyaltyTransactions.Count);
        Assert.Equal(Now, customer.UpdatedAt);
    }

    [Fact]
    public void UnknownOriginalAndOriginalFromAnotherCustomer_AreRejected()
    {
        var customer = CreateCustomer();
        var other = CreateCustomer();
        var foreign = other.EarnLoyaltyPoints(EntityId.New(), 20, Now);

        Assert.Throws<KeyNotFoundException>(() => customer.ReverseLoyaltyTransaction(EntityId.New(), foreign.Id, Later));
        Assert.Throws<KeyNotFoundException>(() => customer.ReverseLoyaltyTransaction(EntityId.New(), EntityId.New(), Later));
        Assert.Empty(customer.LoyaltyTransactions);
        Assert.Equal(0, customer.LoyaltyPointsBalance);
        Assert.Equal(20, other.LoyaltyPointsBalance);
    }

    [Fact]
    public void ReversalOfReversalAndReusedIds_AreRejectedWithoutMutation()
    {
        var customer = CreateCustomer();
        var original = customer.EarnLoyaltyPoints(EntityId.New(), 20, Now);
        Assert.Throws<InvalidOperationException>(() => customer.ReverseLoyaltyTransaction(original.Id, original.Id, Later));
        var reversal = customer.ReverseLoyaltyTransaction(EntityId.New(), original.Id, Now);
        var other = customer.EarnLoyaltyPoints(EntityId.New(), 10, Now);

        Assert.Throws<InvalidOperationException>(() => customer.ReverseLoyaltyTransaction(EntityId.New(), reversal.Id, Later));
        Assert.Throws<InvalidOperationException>(() => customer.ReverseLoyaltyTransaction(reversal.Id, other.Id, Later));
        Assert.Throws<InvalidOperationException>(() => customer.ReverseLoyaltyTransaction(other.Id, original.Id, Later));
        Assert.Equal(10, customer.LoyaltyPointsBalance);
        Assert.Equal(3, customer.LoyaltyTransactions.Count);
        Assert.Equal(Now, customer.UpdatedAt);
    }

    private static Customer CreateCustomer() => new(EntityId.New(), "Alice", "0901234567", Now, Now);
}
