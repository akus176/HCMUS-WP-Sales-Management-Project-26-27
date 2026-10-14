namespace CoffeeShop.Application.Tests.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public sealed class CustomerOrderSelectionTests
{
    [Fact]
    public async Task WalkIn_DoesNotRequireCustomerOrSavedAddress()
    {
        var handler = new CustomerOrderSelectionHandler(new FakeCustomerRepository());
        var takeaway = await handler.PrepareAsync(new());
        var delivery = await handler.PrepareAsync(new(IsDelivery: true,
            RecipientName: " Guest ", ContactNumber: "0901234567", AddressLine: " 10 Main Street "));

        Assert.True(takeaway.IsSuccess);
        Assert.Null(takeaway.Value.CustomerId);
        Assert.Null(takeaway.Value.Delivery);
        Assert.True(delivery.IsSuccess);
        Assert.Null(delivery.Value.CustomerId);
        Assert.True(delivery.Value.Delivery!.IsOrderSpecific);
        Assert.Equal("10 Main Street", delivery.Value.AddressLine);
        Assert.Equal("Guest", delivery.Value.Delivery.RecipientName);
    }

    [Fact]
    public async Task SavedAddress_IsResolvedFromItsActiveOwner()
    {
        var repository = Repository();
        var customer = repository.DetailResult!;
        var handler = new CustomerOrderSelectionHandler(repository);
        var result = await handler.PrepareAsync(new(customer.Id, true, customer.Addresses[0].Id,
            "Alice", "0901234567", "Untrusted address"));

        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value.CustomerId);
        Assert.Equal("1 Main Street", result.Value.AddressLine);
        Assert.Equal(customer.Addresses[0].Id, result.Value.Delivery!.CustomerAddressId);
    }

    [Fact]
    public async Task InactiveCustomerAndStaleOrForeignAddress_ReturnConflict()
    {
        var repository = Repository();
        var customer = repository.DetailResult!;
        var handler = new CustomerOrderSelectionHandler(repository);
        var foreign = await handler.PrepareAsync(new(customer.Id, true, EntityId.New(), "Alice", "0901234567"));
        Assert.Equal(ErrorType.Conflict, foreign.Error!.Type);

        repository.DetailResult = customer with { Addresses = [customer.Addresses[0] with { IsActive = false }] };
        var inactiveAddress = await handler.PrepareAsync(new(customer.Id, true, customer.Addresses[0].Id, "Alice", "0901234567"));
        Assert.Equal(ErrorType.Conflict, inactiveAddress.Error!.Type);

        repository.DetailResult = customer with { IsActive = false };
        var inactive = await handler.PrepareAsync(new(customer.Id));
        Assert.Equal(ErrorType.Conflict, inactive.Error!.Type);
    }

    [Theory]
    [InlineData("", "0901234567", "Address")]
    [InlineData("Alice", "bad phone", "Address")]
    [InlineData("Alice", "0901234567", "")]
    public async Task InvalidDelivery_ReturnsFieldValidation(string name, string phone, string address)
    {
        var result = await new CustomerOrderSelectionHandler(new FakeCustomerRepository())
            .PrepareAsync(new(IsDelivery: true, RecipientName: name, ContactNumber: phone, AddressLine: address));

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.NotEmpty(result.Error.Details);
    }

    [Fact]
    public async Task CreateDraft_RevalidatesSelectionAndSnapshotsSavedAddress()
    {
        var repository = Repository();
        var customer = repository.DetailResult!;
        var handler = new CustomerOrderSelectionHandler(repository);
        var selection = new CustomerOrderSelectionRequest(customer.Id, true, customer.Addresses[0].Id, "Alice", "0901234567");
        Assert.True((await handler.PrepareAsync(selection)).IsSuccess);

        repository.DetailResult = customer with { Addresses = [customer.Addresses[0] with { AddressLine = "2 Main Street" }] };
        var order = (await handler.CreateDraftAsync(Draft("delivery"), selection)).Value;
        Assert.Equal(customer.Id, order.Order.CustomerId);
        Assert.Equal("2 Main Street", order.Delivery!.AddressLine);
        Assert.Equal(customer.Addresses[0].Id, order.Delivery.CustomerAddressId);

        repository.DetailResult = customer with { IsActive = false };
        Assert.Equal(ErrorType.Conflict, (await handler.CreateDraftAsync(Draft("delivery"), selection)).Error!.Type);
    }

    [Fact]
    public async Task CreateDraft_AllowsWalkInAndRejectsChannelMismatch()
    {
        var handler = new CustomerOrderSelectionHandler(new FakeCustomerRepository());
        Assert.Null((await handler.CreateDraftAsync(Draft("takeaway"), new())).Value.Order.CustomerId);
        var selection = new CustomerOrderSelectionRequest(IsDelivery: true,
            RecipientName: "Guest", ContactNumber: "0901234567", AddressLine: "1 Main Street");
        var delivery = (await handler.CreateDraftAsync(Draft("delivery"), selection)).Value;
        Assert.Null(delivery.Order.CustomerId);
        Assert.True(delivery.Delivery!.IsOrderSpecific);
        Assert.Equal(ErrorType.Validation, (await handler.CreateDraftAsync(Draft("takeaway"), selection)).Error!.Type);
    }

    private static CreateOrderDraftRequest Draft(string channel)
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var category = new Category(EntityId.New(), "Coffee", now, now);
        var item = new MenuItem(EntityId.New(), category, "Latte", now, now);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Regular", new Money(100m));
        return new(EntityId.New(), "ORD-SELECT", channel, EntityId.New(), now, [new(item, variant, 1, [])]);
    }

    private static FakeCustomerRepository Repository()
    {
        var now = DateTimeOffset.UtcNow;
        return new FakeCustomerRepository
        {
            DetailResult = new(EntityId.New(), "Alice", "0901234567", null, true, 0, now, now,
                [new(EntityId.New(), "1 Main Street", "Home", true, true)], [])
        };
    }
}
