namespace CoffeeShop.Domain.Tests.Customers;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

public class CustomerAddressTests
{
    private static UtcTimestamp Now => new(DateTimeOffset.UtcNow);

    private static Customer CreateCustomer() =>
        new(EntityId.New(), "Alice Smith", "+1234567890", Now, Now);

    [Fact]
    public void AddAddress_FirstAddress_AutomaticallyBecomesDefault()
    {
        var customer = CreateCustomer();
        var addressId = EntityId.New();

        var address = customer.AddAddress(addressId, " 123 Main Street ", " Home ", isDefault: false);

        Assert.Equal(addressId, address.Id);
        Assert.Equal(customer.Id, address.CustomerId);
        Assert.Equal("123 Main Street", address.AddressLine);
        Assert.Equal("Home", address.Label);
        Assert.True(address.IsDefault);
        Assert.True(address.IsActive);
        Assert.Single(customer.Addresses);
        Assert.Same(address, customer.GetDefaultAddress());
    }

    [Fact]
    public void AddAddress_SecondAddressAsDefault_UnsetsPreviousDefault()
    {
        var customer = CreateCustomer();
        var first = customer.AddAddress(EntityId.New(), "123 Main St", "Home", isDefault: true);
        var second = customer.AddAddress(EntityId.New(), "456 Office Rd", "Work", isDefault: true);

        Assert.False(first.IsDefault);
        Assert.True(second.IsDefault);
        Assert.Same(second, customer.GetDefaultAddress());
        Assert.Equal(2, customer.Addresses.Count);
    }

    [Fact]
    public void AddAddress_SecondAddressNotDefault_KeepsFirstAsDefault()
    {
        var customer = CreateCustomer();
        var first = customer.AddAddress(EntityId.New(), "123 Main St", "Home", isDefault: true);
        var second = customer.AddAddress(EntityId.New(), "456 Office Rd", "Work", isDefault: false);

        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);
        Assert.Same(first, customer.GetDefaultAddress());
    }

    [Fact]
    public void AddAddress_WithDuplicateId_ThrowsInvalidOperationException()
    {
        var customer = CreateCustomer();
        var addressId = EntityId.New();
        customer.AddAddress(addressId, "123 Main St");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            customer.AddAddress(addressId, "456 Other St"));

        Assert.Contains(addressId.ToString(), ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAddress_WithEmptyAddressLine_ThrowsArgumentException(string invalidAddress)
    {
        var customer = CreateCustomer();
        Assert.Throws<ArgumentException>(() => customer.AddAddress(EntityId.New(), invalidAddress));
    }

    [Fact]
    public void AddAddress_AddressLineBoundary500Chars_Succeeds()
    {
        var customer = CreateCustomer();
        var exact500 = new string('A', 500);

        var address = customer.AddAddress(EntityId.New(), exact500);

        Assert.Equal(exact500, address.AddressLine);
    }

    [Fact]
    public void AddAddress_AddressLineExceeding500Chars_ThrowsArgumentException()
    {
        var customer = CreateCustomer();
        var tooLong = new string('A', 501);

        Assert.Throws<ArgumentException>(() => customer.AddAddress(EntityId.New(), tooLong));
    }

    [Fact]
    public void AddAddress_LabelBoundary40Chars_Succeeds()
    {
        var customer = CreateCustomer();
        var exact40 = new string('L', 40);

        var address = customer.AddAddress(EntityId.New(), "123 Main St", exact40);

        Assert.Equal(exact40, address.Label);
    }

    [Fact]
    public void AddAddress_LabelExceeding40Chars_ThrowsArgumentException()
    {
        var customer = CreateCustomer();
        var tooLongLabel = new string('L', 41);

        Assert.Throws<ArgumentException>(() => customer.AddAddress(EntityId.New(), "123 Main St", tooLongLabel));
    }

    [Fact]
    public void AddAddress_WhitespaceLabel_NormalizesToNull()
    {
        var customer = CreateCustomer();

        var address = customer.AddAddress(EntityId.New(), "123 Main St", "   ");

        Assert.Null(address.Label);
    }

    [Fact]
    public void UpdateAddress_ValidData_UpdatesPropertiesAndDefault()
    {
        var customer = CreateCustomer();
        var first = customer.AddAddress(EntityId.New(), "123 Main St", "Home", isDefault: true);
        var second = customer.AddAddress(EntityId.New(), "456 Office Rd", "Work", isDefault: false);

        customer.UpdateAddress(second.Id, "789 New Rd", "HQ", isDefault: true);

        Assert.Equal("789 New Rd", second.AddressLine);
        Assert.Equal("HQ", second.Label);
        Assert.True(second.IsDefault);
        Assert.False(first.IsDefault);
        Assert.Same(second, customer.GetDefaultAddress());
    }

    [Fact]
    public void UpdateAddress_DeactivatedAddressAsDefault_ThrowsInvalidOperationException()
    {
        var customer = CreateCustomer();
        var address = customer.AddAddress(EntityId.New(), "123 Main St", "Home");
        customer.DeactivateAddress(address.Id);

        Assert.Throws<InvalidOperationException>(() =>
            customer.UpdateAddress(address.Id, "123 Main St", "Home", isDefault: true));
    }

    [Fact]
    public void UpdateAddress_AddressNotBelongingToCustomer_ThrowsInvalidOperationException()
    {
        var customer1 = CreateCustomer();
        var customer2 = CreateCustomer();
        var addressOnCustomer2 = customer2.AddAddress(EntityId.New(), "456 Elm St");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            customer1.UpdateAddress(addressOnCustomer2.Id, "New Line", null, false));

        Assert.Contains(customer1.Id.ToString(), ex.Message);
    }

    [Fact]
    public void SetDefaultAddress_ActiveAddress_UnsetsPreviousDefault()
    {
        var customer = CreateCustomer();
        var first = customer.AddAddress(EntityId.New(), "123 Main St", "Home", isDefault: true);
        var second = customer.AddAddress(EntityId.New(), "456 Office Rd", "Work", isDefault: false);

        customer.SetDefaultAddress(second.Id);

        Assert.False(first.IsDefault);
        Assert.True(second.IsDefault);
        Assert.Same(second, customer.GetDefaultAddress());
    }

    [Fact]
    public void SetDefaultAddress_DeactivatedAddress_ThrowsInvalidOperationException()
    {
        var customer = CreateCustomer();
        var address = customer.AddAddress(EntityId.New(), "123 Main St");
        customer.DeactivateAddress(address.Id);

        Assert.Throws<InvalidOperationException>(() => customer.SetDefaultAddress(address.Id));
    }

    [Fact]
    public void SetDefaultAddress_CrossCustomerBoundary_ThrowsInvalidOperationException()
    {
        var customer1 = CreateCustomer();
        var customer2 = CreateCustomer();
        var addressOnCustomer2 = customer2.AddAddress(EntityId.New(), "456 Elm St");

        Assert.Throws<InvalidOperationException>(() =>
            customer1.SetDefaultAddress(addressOnCustomer2.Id));
    }

    [Fact]
    public void DeactivateAddress_SetsIsActiveFalseAndClearsDefault()
    {
        var customer = CreateCustomer();
        var address = customer.AddAddress(EntityId.New(), "123 Main St", "Home", isDefault: true);

        customer.DeactivateAddress(address.Id);

        Assert.False(address.IsActive);
        Assert.False(address.IsDefault);
        Assert.Null(customer.GetDefaultAddress());
        Assert.Empty(customer.GetActiveAddresses());
    }

    [Fact]
    public void DeactivateAddress_CrossCustomerBoundary_ThrowsInvalidOperationException()
    {
        var customer1 = CreateCustomer();
        var customer2 = CreateCustomer();
        var addressOnCustomer2 = customer2.AddAddress(EntityId.New(), "456 Elm St");

        Assert.Throws<InvalidOperationException>(() =>
            customer1.DeactivateAddress(addressOnCustomer2.Id));
    }

    [Fact]
    public void ActivateAddress_ReactivatesAddress_AndBecomesDefaultIfNoDefaultExists()
    {
        var customer = CreateCustomer();
        var address = customer.AddAddress(EntityId.New(), "123 Main St", "Home", isDefault: true);
        customer.DeactivateAddress(address.Id);

        Assert.Null(customer.GetDefaultAddress());

        customer.ActivateAddress(address.Id);

        Assert.True(address.IsActive);
        Assert.True(address.IsDefault);
        Assert.Same(address, customer.GetDefaultAddress());
    }

    [Fact]
    public void ActivateAddress_WhenAnotherDefaultExists_RemainsNonDefault()
    {
        var customer = CreateCustomer();
        var first = customer.AddAddress(EntityId.New(), "123 Main St", "Home", isDefault: true);
        var second = customer.AddAddress(EntityId.New(), "456 Office Rd", "Work", isDefault: false);
        customer.DeactivateAddress(second.Id);

        customer.ActivateAddress(second.Id);

        Assert.True(second.IsActive);
        Assert.False(second.IsDefault);
        Assert.True(first.IsDefault);
        Assert.Same(first, customer.GetDefaultAddress());
    }

    [Fact]
    public void GetActiveAddresses_ReturnsOnlyActiveAddresses()
    {
        var customer = CreateCustomer();
        var first = customer.AddAddress(EntityId.New(), "123 Main St", "Home");
        var second = customer.AddAddress(EntityId.New(), "456 Office Rd", "Work");
        customer.DeactivateAddress(first.Id);

        var active = customer.GetActiveAddresses();

        Assert.Single(active);
        Assert.Equal(second.Id, active[0].Id);
    }
}
