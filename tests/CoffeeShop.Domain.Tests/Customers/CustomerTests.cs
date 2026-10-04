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
    public void Customer_WithNameExceeding120Chars_ShouldThrow()
    {
        var id = EntityId.New();
        var longName = new string('A', 121);
        Assert.Throws<ArgumentException>(() => new Customer(id, longName, "+1234567890", Now, Now));
    }

    [Theory]
    [InlineData("invalid-phone")]
    [InlineData("12345")] // too short (< 7 digits)
    [InlineData("+1234567890123456789012345678901")] // > 30 characters
    [InlineData("phone!@#$")]
    public void Customer_WithInvalidPhone_ShouldThrow(string invalidPhone)
    {
        var id = EntityId.New();
        Assert.Throws<ArgumentException>(() => new Customer(id, "Valid Name", invalidPhone, Now, Now));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("plainaddress")]
    [InlineData("@missingusername.com")]
    [InlineData("username@.com")]
    public void Customer_WithInvalidEmail_ShouldThrow(string invalidEmail)
    {
        var id = EntityId.New();
        Assert.Throws<ArgumentException>(() => new Customer(id, "Valid Name", "+1234567890", Now, Now, invalidEmail));
    }

    [Fact]
    public void Customer_WithEmailExceeding255Chars_ShouldThrow()
    {
        var id = EntityId.New();
        var longEmail = $"{new string('a', 250)}@test.com";
        Assert.Throws<ArgumentException>(() => new Customer(id, "Valid Name", "+1234567890", Now, Now, longEmail));
    }

    [Fact]
    public void Customer_Update_ShouldUpdatePropertiesAndPreserveActiveState()
    {
        var id = EntityId.New();
        var created = Now;
        var changed = new UtcTimestamp(created.Value.AddHours(2));
        var customer = new Customer(id, "Alice Smith", "+1234567890", created, created, "alice@example.com", isActive: false);

        customer.Update("Alice Jones", "0901234567", "alice.jones@example.com", changed);

        Assert.Equal("Alice Jones", customer.FullName);
        Assert.Equal("0901234567", customer.Phone);
        Assert.Equal("alice.jones@example.com", customer.Email);
        Assert.Equal(changed, customer.UpdatedAt);
        Assert.False(customer.IsActive); // Updating an inactive customer preserves inactive status
    }

    [Fact]
    public void Customer_Update_WithInvalidData_ShouldThrow()
    {
        var id = EntityId.New();
        var customer = new Customer(id, "Alice Smith", "+1234567890", Now, Now);

        Assert.Throws<ArgumentException>(() => customer.Update("", "+1234567890", null, Now));
        Assert.Throws<ArgumentException>(() => customer.Update("Alice", "not-a-phone", null, Now));
        Assert.Throws<ArgumentException>(() => customer.Update("Alice", "+1234567890", "bad-email", Now));
    }

    [Fact]
    public void Customer_Deactivate_ShouldSetIsActiveFalse()
    {
        var id = EntityId.New();
        var created = Now;
        var changed = new UtcTimestamp(created.Value.AddHours(1));
        var customer = new Customer(id, "Alice Smith", "+1234567890", created, created, isActive: true);

        customer.Deactivate(changed);

        Assert.False(customer.IsActive);
        Assert.Equal(changed, customer.UpdatedAt);
    }

    [Fact]
    public void Customer_Activate_ShouldSetIsActiveTrue()
    {
        var id = EntityId.New();
        var created = Now;
        var changed = new UtcTimestamp(created.Value.AddHours(1));
        var customer = new Customer(id, "Alice Smith", "+1234567890", created, created, isActive: false);

        customer.Activate(changed);

        Assert.True(customer.IsActive);
        Assert.Equal(changed, customer.UpdatedAt);
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
