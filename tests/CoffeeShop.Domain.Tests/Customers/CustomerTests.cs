namespace CoffeeShop.Domain.Tests.Customers;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

public class CustomerTests
{
    private static UtcTimestamp Now => new(DateTimeOffset.UtcNow);

    [Fact]
    public void Customer_WithValidData_ShouldInitializeCorrectly()
    {
        var id = EntityId.New();
        var customer = new Customer(id, "Alice Smith", "+1234567890", Now, Now, "alice@example.com", true, 100);

        Assert.Equal(id, customer.Id);
        Assert.Equal("Alice Smith", customer.FullName);
        Assert.Equal("+1234567890", customer.Phone);
        Assert.Equal("alice@example.com", customer.Email);
        Assert.True(customer.IsActive);
        Assert.Equal(100, customer.LoyaltyPointsBalance);
    }

    [Fact]
    public void Customer_WithNegativeBalance_ShouldThrow()
    {
        var id = EntityId.New();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Customer(id, "Bob", "+1234567890", Now, Now, loyaltyPointsBalance: -10));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Customer_WithInvalidPhoneOrName_ShouldThrow(string invalidInput)
    {
        var id = EntityId.New();
        Assert.Throws<ArgumentException>(() => new Customer(id, invalidInput, "+1234567890", Now, Now));
        Assert.Throws<ArgumentException>(() => new Customer(id, "Valid Name", invalidInput, Now, Now));
    }

    [Fact]
    public void LoyaltyTransaction_WithInvalidType_ShouldThrow()
    {
        var id = EntityId.New();
        var customerId = EntityId.New();

        Assert.Throws<ArgumentException>(() =>
            new LoyaltyTransaction(id, customerId, "invalid_type", 10, 10, Now));
    }

    [Fact]
    public void LoyaltyTransaction_WithZeroPoints_ShouldThrow()
    {
        var id = EntityId.New();
        var customerId = EntityId.New();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoyaltyTransaction(id, customerId, LoyaltyTransactionType.Earn, 0, 10, Now));
    }

    [Fact]
    public void LoyaltyTransaction_WithNegativeBalanceAfter_ShouldThrow()
    {
        var id = EntityId.New();
        var customerId = EntityId.New();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoyaltyTransaction(id, customerId, LoyaltyTransactionType.Redeem, -10, -5, Now));
    }

    [Fact]
    public void LoyaltySettings_WithInvalidValues_ShouldThrow()
    {
        var id = EntityId.New();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoyaltySettings(id, earnPointsPerCurrencyUnit: -1, redeemValuePerPoint: 1, maxRedeemablePercentage: 50, updatedAt: Now));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoyaltySettings(id, earnPointsPerCurrencyUnit: 1, redeemValuePerPoint: -1, maxRedeemablePercentage: 50, updatedAt: Now));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoyaltySettings(id, earnPointsPerCurrencyUnit: 1, redeemValuePerPoint: 1, maxRedeemablePercentage: 150, updatedAt: Now));
    }
}
