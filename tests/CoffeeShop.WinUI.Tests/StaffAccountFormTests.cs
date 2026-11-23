using CoffeeShop.Contracts;
using CoffeeShop.WinUI.ViewModels;

namespace CoffeeShop.WinUI.Tests;

public sealed class StaffAccountFormTests
{
    [Theory]
    [InlineData("", "new.cashier", "new@example.test", "Cashier", "valid-initial-password", "full name")]
    [InlineData("New Cashier", "", "new@example.test", "Cashier", "valid-initial-password", "username")]
    [InlineData("New Cashier", "new.cashier", "invalid", "Cashier", "valid-initial-password", "email")]
    [InlineData("New Cashier", "new.cashier", "new@example.test", "Unknown", "valid-initial-password", "role")]
    [InlineData("New Cashier", "new.cashier", "new@example.test", "Cashier", "short", "password")]
    public void Validate_RejectsIncompleteStaffDetails(
        string name, string username, string email, string role, string password, string expectedField)
    {
        var error = StaffAccountForm.Validate(new CreateStaffRequest(name, username, email, null, role, password));
        Assert.Contains(expectedField, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AcceptsOperationalRoleAndValidContact()
    {
        Assert.Null(StaffAccountForm.Validate(new CreateStaffRequest(
            "New Cashier", "new.cashier", "new@example.test", "+84901234567", "Cashier", "valid-initial-password")));
    }
}
