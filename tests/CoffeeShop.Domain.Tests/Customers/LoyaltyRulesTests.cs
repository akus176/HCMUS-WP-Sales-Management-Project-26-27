namespace CoffeeShop.Domain.Tests.Customers;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;

public class LoyaltyRulesTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));

    public static TheoryData<decimal, decimal, int> EarningCases => new()
    {
        { 125m, 0.1m, 12 },
        { 9.99m, 0.1m, 0 },
        { 10m, 0.1m, 1 },
        { 45000m, 0.0001m, 4 },
        { 0m, 1m, 0 },
        { 100m, 0m, 0 },
    };

    [Theory]
    [MemberData(nameof(EarningCases))]
    public void Earning_UsesConfiguredRateAndRoundsDown(decimal total, decimal rate, int expected)
    {
        var customer = CreateCustomer();
        var order = new StoredOrder(customer.Id, total, OrderStatus.Completed, total);

        Assert.Equal(expected, LoyaltyRules.CalculateEarnedPoints(customer, order, Settings(rate)));
        Assert.Equal(0, customer.LoyaltyPointsBalance);
        Assert.Empty(customer.LoyaltyTransactions);
    }

    [Fact]
    public void Earning_UsesNetTotalAfterDiscountsAndIgnoresOverpayment()
    {
        var customer = CreateCustomer(20);
        var order = new StoredOrder(customer.Id, 65m, OrderStatus.Completed, 100m,
            redeemed: 30, loyaltyDiscount: 15m, subtotal: 100m, ordinaryDiscount: 20m);

        Assert.Equal(6, LoyaltyRules.CalculateEarnedPoints(customer, order, Settings(0.1m)));
        Assert.Equal(20, customer.LoyaltyPointsBalance);
        Assert.Equal(65m, order.Total.Amount);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.Cancelled)]
    public void Earning_RejectsUncompletedOrdersEvenWhenFullyPaid(string status)
    {
        var customer = CreateCustomer();
        var order = new StoredOrder(customer.Id, 100m, status, 100m);

        Assert.Throws<InvalidOperationException>(() => LoyaltyRules.CalculateEarnedPoints(customer, order, Settings()));
    }

    [Fact]
    public void Earning_RejectsUnderpaidOrder()
    {
        var customer = CreateCustomer();
        var order = new StoredOrder(customer.Id, 100m, OrderStatus.Completed, 99.99m);

        Assert.Throws<InvalidOperationException>(() => LoyaltyRules.CalculateEarnedPoints(customer, order, Settings()));
    }

    [Fact]
    public void Earning_RejectsPointOrBalanceOverflowAndAllowsExactLimit()
    {
        var customer = CreateCustomer(int.MaxValue - 1);
        var order = new StoredOrder(customer.Id, 1m, OrderStatus.Completed, 1m);
        Assert.Equal(1, LoyaltyRules.CalculateEarnedPoints(customer, order, Settings()));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltyRules.CalculateEarnedPoints(customer, order, Settings(2m)));

        var emptyCustomer = CreateCustomer();
        var largeOrder = new StoredOrder(emptyCustomer.Id, Money.MaximumAmount, OrderStatus.Completed, Money.MaximumAmount);
        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltyRules.CalculateEarnedPoints(emptyCustomer, largeOrder, Settings(9999.9999m)));
        Assert.Equal(int.MaxValue - 1, customer.LoyaltyPointsBalance);
    }

    public static TheoryData<int, decimal, decimal, decimal, int> RedemptionLimits => new()
    {
        { 30, 100m, 0.5m, 20m, 30 }, // Balance limits the quote.
        { 100, 100m, 0.5m, 20m, 40 }, // Percentage limits the quote.
        { 100, 100m, 0.5m, 0m, 0 },
        { 100, 100m, 0m, 20m, 0 },
        { 100, 0m, 0.5m, 20m, 0 },
        { 0, 100m, 0.5m, 20m, 0 },
        { 100, 10m, 0.3333m, 100m, 30 },
        { 100, 0.05m, 0.01m, 50m, 2 }, // Half-cent cap rounds down.
        { 100, 0.03m, 0.015m, 50m, 0 }, // Exact conversion must fit the rounded cap.
        { 99, 100m, 0.0001m, 100m, 0 }, // Never burn points for zero money.
        { 100, 100m, 0.0001m, 100m, 100 },
        { int.MaxValue, Money.MaximumAmount, 0.0001m, 100m, int.MaxValue },
    };

    [Theory]
    [MemberData(nameof(RedemptionLimits))]
    public void MaximumRedemption_RespectsBalanceConversionAndCap(int balance, decimal total, decimal rate, decimal percentage, int expected)
    {
        var customer = CreateCustomer(balance);
        var order = new StoredOrder(customer.Id, total);

        Assert.Equal(expected, LoyaltyRules.CalculateMaximumRedeemablePoints(customer, order, Settings(redeem: rate, cap: percentage)));
        Assert.Equal(balance, customer.LoyaltyPointsBalance);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Confirmed)]
    public void Redemption_AllowsExactCapWithoutMutatingOrderOrBalance(string status)
    {
        var customer = CreateCustomer(100);
        var order = new StoredOrder(customer.Id, 80m, status, subtotal: 100m, ordinaryDiscount: 20m);
        var settings = Settings(redeem: 0.5m, cap: 20m);

        Assert.Equal(new Money(16m), LoyaltyRules.CalculateRedemptionDiscount(customer, order, settings, 32));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltyRules.CalculateRedemptionDiscount(customer, order, settings, 33));
        Assert.Equal(100, customer.LoyaltyPointsBalance);
        Assert.Empty(customer.LoyaltyTransactions);
        Assert.Equal(80m, order.Total.Amount);
        Assert.Equal(0, order.LoyaltyPointsRedeemed);
        Assert.Equal(Money.Zero, order.LoyaltyDiscountAmount);
    }

    [Fact]
    public void Redemption_ConvertsFractionalRateAndRoundsDownMoney()
    {
        var customer = CreateCustomer(100);
        var order = new StoredOrder(customer.Id, 10m);
        var settings = Settings(redeem: 0.3333m, cap: 100m);

        Assert.Equal(new Money(0.99m), LoyaltyRules.CalculateRedemptionDiscount(customer, order, settings, 3));
        Assert.Equal(new Money(9.99m), LoyaltyRules.CalculateRedemptionDiscount(customer, order, settings, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltyRules.CalculateRedemptionDiscount(customer, order, settings, 31));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(int.MaxValue)]
    public void Redemption_RejectsNonpositiveOrUnavailablePoints(int points)
    {
        var customer = CreateCustomer(30);
        var order = new StoredOrder(customer.Id, 100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltyRules.CalculateRedemptionDiscount(customer, order, Settings(), points));
    }

    [Fact]
    public void Redemption_RejectsDisabledRateAndSubCentDiscounts()
    {
        var customer = CreateCustomer(100);
        var order = new StoredOrder(customer.Id, 100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltyRules.CalculateRedemptionDiscount(customer, order, Settings(redeem: 0m), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoyaltyRules.CalculateRedemptionDiscount(customer, order, Settings(redeem: 0.0001m), 99));
        Assert.Equal(new Money(0.01m), LoyaltyRules.CalculateRedemptionDiscount(customer, order, Settings(redeem: 0.0001m), 100));
    }

    [Theory]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void Redemption_RejectsIneligibleOrderStates(string status)
    {
        var customer = CreateCustomer(100);
        var order = new StoredOrder(customer.Id, 100m, status);

        Assert.Throws<InvalidOperationException>(() => LoyaltyRules.CalculateMaximumRedeemablePoints(customer, order, Settings()));
        Assert.Throws<InvalidOperationException>(() => LoyaltyRules.CalculateRedemptionDiscount(customer, order, Settings(), 1));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void Redemption_RejectsAlreadyAppliedRedemption(int redeemed, int discount)
    {
        var customer = CreateCustomer(100);
        var order = new StoredOrder(customer.Id, 100m, redeemed: redeemed, loyaltyDiscount: discount);

        Assert.Throws<InvalidOperationException>(() => LoyaltyRules.CalculateRedemptionDiscount(customer, order, Settings(), 1));
    }

    [Fact]
    public void InactiveCustomer_CannotEarnOrRedeem()
    {
        var customer = CreateCustomer(100);
        customer.Deactivate(Now);
        var completed = new StoredOrder(customer.Id, 100m, OrderStatus.Completed, 100m);
        var draft = new StoredOrder(customer.Id, 100m);

        Assert.Throws<InvalidOperationException>(() => LoyaltyRules.CalculateEarnedPoints(customer, completed, Settings()));
        Assert.Throws<InvalidOperationException>(() => LoyaltyRules.CalculateMaximumRedeemablePoints(customer, draft, Settings()));
        Assert.Throws<InvalidOperationException>(() => LoyaltyRules.CalculateRedemptionDiscount(customer, draft, Settings(), 1));
        Assert.Equal(100, customer.LoyaltyPointsBalance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnattachedOrDifferentCustomer_CannotEarnOrRedeem(bool walkIn)
    {
        var customer = CreateCustomer(100);
        var attached = walkIn ? null : EntityId.New();
        var completed = new StoredOrder(attached, 100m, OrderStatus.Completed, 100m);
        var draft = new StoredOrder(attached, 100m);

        Assert.Throws<ArgumentException>(() => LoyaltyRules.CalculateEarnedPoints(customer, completed, Settings()));
        Assert.Throws<ArgumentException>(() => LoyaltyRules.CalculateRedemptionDiscount(customer, draft, Settings(), 1));
    }

    private static Customer CreateCustomer(int balance = 0) =>
        new(EntityId.New(), "Alice", "0901234567", Now, Now, loyaltyPointsBalance: balance);

    private static LoyaltySettings Settings(decimal earn = 1m, decimal redeem = 0.5m, decimal cap = 20m) =>
        new(EntityId.New(), earn, redeem, cap, Now);

    // Represents order state loaded by persistence; payment/discount commands belong to the sales workflow.
    private sealed class StoredOrder : Order
    {
        public StoredOrder(EntityId? customerId, decimal total, string status = OrderStatus.Draft, decimal paid = 0m,
            int redeemed = 0, decimal loyaltyDiscount = 0m, decimal? subtotal = null, decimal ordinaryDiscount = 0m)
        {
            CustomerId = customerId;
            Subtotal = new Money(subtotal ?? total);
            DiscountTotal = new Money(ordinaryDiscount);
            Total = new Money(total);
            PaidAmount = new Money(paid);
            Status = status;
            LoyaltyPointsRedeemed = redeemed;
            LoyaltyDiscountAmount = new Money(loyaltyDiscount);
        }
    }
}
