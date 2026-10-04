namespace CoffeeShop.Application.Tests.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

public class CustomerLifecycleTests
{
    private static readonly UtcTimestamp Created = new(new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp Changed = new(Created.Value.AddHours(1));

    [Fact]
    public void CreateCustomer_WithValidData_ReturnsSuccess()
    {
        var result = CustomerLifecycle.CreateCustomer(
            EntityId.New(),
            "  Alice Smith  ",
            " 0901234567 ",
            Created,
            " ALICE@example.com ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Alice Smith", result.Value.FullName);
        Assert.Equal("0901234567", result.Value.Phone);
        Assert.Equal("alice@example.com", result.Value.Email);
        Assert.True(result.Value.IsActive);
        Assert.Equal(0, result.Value.LoyaltyPointsBalance);
        Assert.Equal(Created, result.Value.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateCustomer_WithInvalidName_ReturnsValidationError(string? name)
    {
        var result = CustomerLifecycle.CreateCustomer(EntityId.New(), name!, "0901234567", Created);

        AssertValidation(result, "fullName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-phone")]
    [InlineData("123")]
    public void CreateCustomer_WithInvalidPhone_ReturnsValidationError(string? phone)
    {
        var result = CustomerLifecycle.CreateCustomer(EntityId.New(), "Alice Smith", phone!, Created);

        AssertValidation(result, "phone");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("alice@")]
    public void CreateCustomer_WithInvalidEmail_ReturnsValidationError(string invalidEmail)
    {
        var result = CustomerLifecycle.CreateCustomer(EntityId.New(), "Alice Smith", "0901234567", Created, invalidEmail);

        AssertValidation(result, "email");
    }

    [Fact]
    public void CreateCustomer_WithDuplicatePhone_ReturnsConflictError()
    {
        var phone = "0901234567";
        var result = CustomerLifecycle.CreateCustomer(
            EntityId.New(),
            "Bob Smith",
            phone,
            Created,
            isPhoneTaken: p => p == phone);

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("customers.phone_conflict", result.Error.Code);
        Assert.Equal("phone", Assert.Single(result.Error.Details).Target);
    }

    [Fact]
    public void UpdateCustomer_WithValidData_ReturnsSuccessAndUpdates()
    {
        var customer = CreateCustomer();

        var result = CustomerLifecycle.UpdateCustomer(
            customer,
            "Alice Wonder",
            "0987654321",
            "alice.wonder@example.com",
            Changed);

        Assert.True(result.IsSuccess);
        Assert.Equal("Alice Wonder", customer.FullName);
        Assert.Equal("0987654321", customer.Phone);
        Assert.Equal("alice.wonder@example.com", customer.Email);
        Assert.Equal(Changed, customer.UpdatedAt);
    }

    [Fact]
    public void UpdateCustomer_WithInvalidPhone_ReturnsValidationErrorWithoutMutation()
    {
        var customer = CreateCustomer();

        var result = CustomerLifecycle.UpdateCustomer(
            customer,
            "Alice Wonder",
            "bad-phone",
            null,
            Changed);

        AssertValidation(result, "phone");
        Assert.Equal("Alice Smith", customer.FullName);
        Assert.Equal("0901234567", customer.Phone);
        Assert.Equal(Created, customer.UpdatedAt);
    }

    [Fact]
    public void UpdateCustomer_WithDuplicatePhoneFromAnotherCustomer_ReturnsConflictErrorWithoutMutation()
    {
        var customer = CreateCustomer();
        var conflictingPhone = "0911222333";

        var result = CustomerLifecycle.UpdateCustomer(
            customer,
            "Alice Wonder",
            conflictingPhone,
            null,
            Changed,
            isPhoneTakenByAnother: (phone, id) => phone == conflictingPhone && id != EntityId.New());

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("customers.phone_conflict", result.Error.Code);
        Assert.Equal("phone", Assert.Single(result.Error.Details).Target);
        Assert.Equal("0901234567", customer.Phone);
    }

    [Fact]
    public void UpdateCustomer_WithSamePhoneForSameCustomer_ReturnsSuccess()
    {
        var customer = CreateCustomer();

        var result = CustomerLifecycle.UpdateCustomer(
            customer,
            "Alice Wonder",
            customer.Phone,
            null,
            Changed,
            isPhoneTakenByAnother: (phone, id) => false);

        Assert.True(result.IsSuccess);
        Assert.Equal("Alice Wonder", customer.FullName);
    }

    [Fact]
    public void DeactivateCustomer_ReturnsSuccessAndDeactivates()
    {
        var customer = CreateCustomer();

        var result = CustomerLifecycle.DeactivateCustomer(customer, Changed);

        Assert.True(result.IsSuccess);
        Assert.False(customer.IsActive);
        Assert.Equal(Changed, customer.UpdatedAt);
    }

    [Fact]
    public void ActivateCustomer_ReturnsSuccessAndActivates()
    {
        var customer = CreateCustomer();
        CustomerLifecycle.DeactivateCustomer(customer, Changed);

        var result = CustomerLifecycle.ActivateCustomer(customer, Changed);

        Assert.True(result.IsSuccess);
        Assert.True(customer.IsActive);
        Assert.Equal(Changed, customer.UpdatedAt);
    }

    private static Customer CreateCustomer() =>
        new(EntityId.New(), "Alice Smith", "0901234567", Created, Created, "alice@example.com");

    private static void AssertValidation(Result result, string target)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("customers.invalid_input", result.Error.Code);
        Assert.Equal(target, Assert.Single(result.Error.Details).Target);
    }
}
