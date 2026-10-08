namespace CoffeeShop.Application.Tests.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

public sealed class CustomerCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAndUpdateCustomer_PersistNormalizedProfile()
    {
        var repository = new FakeCustomerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CustomerCommandHandler(repository, unitOfWork, new TestClock(Now));

        var created = await handler.CreateCustomerAsync(new(" Alice Nguyen ", " 0901234567 ", " ALICE@EXAMPLE.COM "));
        var updated = await handler.UpdateCustomerAsync(new(created.Value, "Alice Tran", "0907654321", null));

        Assert.True(created.IsSuccess);
        Assert.True(updated.IsSuccess);
        var customer = repository.Customers[created.Value];
        Assert.Equal("Alice Tran", customer.FullName);
        Assert.Equal("0907654321", customer.Phone);
        Assert.Null(customer.Email);
        Assert.Equal(2, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateCustomer_WithDuplicatePhone_ReturnsConflictWithoutSaving()
    {
        var repository = new FakeCustomerRepository();
        var existing = Customer("0901234567");
        repository.Customers.Add(existing.Id, existing);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CustomerCommandHandler(repository, unitOfWork, new TestClock(Now));

        var result = await handler.CreateCustomerAsync(new("Other", " 0901234567 "));

        AssertError(result, ErrorType.Conflict, "customers.phone_conflict");
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AddressCommands_PreserveSingleDefaultAndHistory()
    {
        var repository = new FakeCustomerRepository();
        var customer = Customer("0901234567");
        repository.Customers.Add(customer.Id, customer);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CustomerCommandHandler(repository, unitOfWork, new TestClock(Now));

        var home = (await handler.AddAddressAsync(new(customer.Id, "1 Main Street", "Home"))).Value;
        var office = (await handler.AddAddressAsync(new(customer.Id, "2 Work Road", "Office", true))).Value;
        Assert.True((await handler.SetDefaultAddressAsync(new(customer.Id, home))).IsSuccess);
        Assert.True((await handler.UpdateAddressAsync(new(customer.Id, office, "22 Work Road", "HQ", false))).IsSuccess);
        Assert.True((await handler.DeactivateAddressAsync(new(customer.Id, home))).IsSuccess);

        Assert.False(customer.GetAddress(home)!.IsActive);
        Assert.False(customer.GetAddress(home)!.IsDefault);
        Assert.Equal("22 Work Road", customer.GetAddress(office)!.AddressLine);
        Assert.Equal(5, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task MissingCustomer_ReturnsNotFoundForProfileAndAddressCommands()
    {
        var handler = new CustomerCommandHandler(
            new FakeCustomerRepository(), new FakeUnitOfWork(), new TestClock(Now));
        var id = EntityId.New();

        AssertError(await handler.UpdateCustomerAsync(new(id, "Alice", "0901234567")),
            ErrorType.NotFound, "customers.customer_not_found");
        AssertError(await handler.AddAddressAsync(new(id, "1 Main Street")),
            ErrorType.NotFound, "customers.customer_not_found");
        AssertError(await handler.DeactivateCustomerAsync(new(id)),
            ErrorType.NotFound, "customers.customer_not_found");
    }

    [Fact]
    public async Task InvalidProfile_ReturnsSharedValidationResult()
    {
        var handler = new CustomerCommandHandler(
            new FakeCustomerRepository(), new FakeUnitOfWork(), new TestClock(Now));

        var result = await handler.CreateCustomerAsync(new("", "bad"));

        AssertError(result, ErrorType.Validation, "customers.invalid_input");
    }

    private static Customer Customer(string phone) => new(
        EntityId.New(), "Alice", phone, new UtcTimestamp(Now), new UtcTimestamp(Now));

    private static void AssertError(Result result, ErrorType type, string code)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(type, result.Error!.Type);
        Assert.Equal(code, result.Error.Code);
    }
}
