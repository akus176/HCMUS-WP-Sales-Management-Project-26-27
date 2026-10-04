namespace CoffeeShop.Application.Tests.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;

public class DeliveryOrderDraftTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void CreateDelivery_WithReusableCustomerAddress_SucceedsAndSnapshotsAddressLine()
    {
        var (item, variant, addon) = CreateCatalog();
        var customer = CreateCustomer("Alice Smith", "0901234567");
        var address = customer.AddAddress(EntityId.New(), "123 Main Street, Apt 4B", "Home", isDefault: true);
        var reader = new InMemoryCustomerDeliveryAddressReader(customer);
        var factory = new OrderDraftFactory(reader);

        var request = new CreateOrderDraftRequest(
            EntityId.New(),
            "ORD-DEL-001",
            "delivery",
            EntityId.New(),
            Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])],
            CustomerId: customer.Id,
            DeliverySelection: DeliverySelection.FromCustomerAddress(address.Id, "Alice Smith", "0901234567"));

        var result = factory.Create(request);

        Assert.True(result.IsSuccess);
        var order = result.Value;
        Assert.Equal("delivery", order.Channel);
        Assert.Equal(customer.Id, order.CustomerId);

        // Find delivery in sales context
        var defaultAddr = reader.GetDefaultAddress(customer.Id);
        Assert.NotNull(defaultAddr);
        Assert.Equal(address.Id, defaultAddr.AddressId);
        Assert.Equal("123 Main Street, Apt 4B", defaultAddr.AddressLine);
    }

    [Fact]
    public void CreateDelivery_CrossCustomerBoundary_ReturnsValidationFailure()
    {
        var (item, variant, addon) = CreateCatalog();
        var customer1 = CreateCustomer("Alice Smith", "0901111111");
        var customer2 = CreateCustomer("Bob Jones", "0902222222");
        var addressOfCustomer2 = customer2.AddAddress(EntityId.New(), "456 Elm St", "Work");

        var reader = new InMemoryCustomerDeliveryAddressReader(customer1, customer2);
        var factory = new OrderDraftFactory(reader);

        // Customer 1 tries to use Customer 2's address
        var request = new CreateOrderDraftRequest(
            EntityId.New(),
            "ORD-DEL-002",
            "delivery",
            EntityId.New(),
            Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])],
            CustomerId: customer1.Id,
            DeliverySelection: DeliverySelection.FromCustomerAddress(addressOfCustomer2.Id, "Alice Smith", "0901111111"));

        var result = factory.Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("orders.invalid", result.Error.Code);
        Assert.Contains("cross customer boundaries", result.Error.Description);
    }

    [Fact]
    public void CreateDelivery_WithDeactivatedCustomerAddress_ReturnsValidationFailure()
    {
        var (item, variant, addon) = CreateCatalog();
        var customer = CreateCustomer("Alice Smith", "0901234567");
        var address = customer.AddAddress(EntityId.New(), "123 Main St", "Old Home");
        customer.DeactivateAddress(address.Id);

        var reader = new InMemoryCustomerDeliveryAddressReader(customer);
        var factory = new OrderDraftFactory(reader);

        var request = new CreateOrderDraftRequest(
            EntityId.New(),
            "ORD-DEL-003",
            "delivery",
            EntityId.New(),
            Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])],
            CustomerId: customer.Id,
            DeliverySelection: DeliverySelection.FromCustomerAddress(address.Id, "Alice", "0901234567"));

        var result = factory.Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("orders.invalid", result.Error.Code);
        Assert.Contains("deactivated", result.Error.Description);
    }

    [Fact]
    public void CreateDelivery_WithUnknownAddressId_ReturnsValidationFailure()
    {
        var (item, variant, addon) = CreateCatalog();
        var customer = CreateCustomer("Alice Smith", "0901234567");
        var unknownId = EntityId.New();

        var reader = new InMemoryCustomerDeliveryAddressReader(customer);
        var factory = new OrderDraftFactory(reader);

        var request = new CreateOrderDraftRequest(
            EntityId.New(),
            "ORD-DEL-004",
            "delivery",
            EntityId.New(),
            Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])],
            CustomerId: customer.Id,
            DeliverySelection: DeliverySelection.FromCustomerAddress(unknownId, "Alice", "0901234567"));

        var result = factory.Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("not found", result.Error.Description);
    }

    [Fact]
    public void CreateDelivery_ReusableAddressWithoutCustomer_ReturnsValidationFailure()
    {
        var (item, variant, addon) = CreateCatalog();
        var reader = new InMemoryCustomerDeliveryAddressReader();
        var factory = new OrderDraftFactory(reader);

        var request = new CreateOrderDraftRequest(
            EntityId.New(),
            "ORD-DEL-005",
            "delivery",
            EntityId.New(),
            Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])],
            CustomerId: null,
            DeliverySelection: DeliverySelection.FromCustomerAddress(EntityId.New(), "Walk In", "0901234567"));

        var result = factory.Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("attached customer", result.Error.Description);
    }

    [Fact]
    public void CreateDelivery_OrderSpecificAddress_WithoutCustomer_SucceedsForWalkIn()
    {
        var (item, variant, addon) = CreateCatalog();
        var factory = new OrderDraftFactory();

        var request = new CreateOrderDraftRequest(
            EntityId.New(),
            "ORD-DEL-006",
            "delivery",
            EntityId.New(),
            Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])],
            CustomerId: null,
            DeliverySelection: DeliverySelection.OrderSpecific("789 Hotel Blvd, Room 101", "Guest User", "0909999999"));

        var result = factory.Create(request);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.CustomerId);
        Assert.Equal("delivery", result.Value.Channel);
    }

    [Fact]
    public void CreateDelivery_OrderSpecificAddress_WithAttachedCustomer_Succeeds()
    {
        var (item, variant, addon) = CreateCatalog();
        var customer = CreateCustomer("Alice Smith", "0901234567");
        var factory = new OrderDraftFactory();

        var request = new CreateOrderDraftRequest(
            EntityId.New(),
            "ORD-DEL-007",
            "delivery",
            EntityId.New(),
            Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])],
            CustomerId: customer.Id,
            DeliverySelection: DeliverySelection.OrderSpecific("Temporary Park Bench 3", "Alice Smith", "0901234567"));

        var result = factory.Create(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value.CustomerId);
    }

    [Theory]
    [InlineData("", "0901234567", "123 Main St")] // empty recipient
    [InlineData("Alice", "", "123 Main St")] // empty contact
    [InlineData("Alice", "not-a-phone", "123 Main St")] // invalid contact
    [InlineData("Alice", "0901234567", "")] // empty address
    public void CreateDelivery_OrderSpecificAddress_WithInvalidFields_ReturnsValidationFailure(
        string recipient,
        string contact,
        string address)
    {
        var (item, variant, addon) = CreateCatalog();
        var factory = new OrderDraftFactory();

        var request = new CreateOrderDraftRequest(
            EntityId.New(),
            "ORD-DEL-008",
            "delivery",
            EntityId.New(),
            Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])],
            DeliverySelection: DeliverySelection.OrderSpecific(address, recipient, contact));

        var result = factory.Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void CreateDelivery_DirectDeliveryWithCrossCustomerBoundary_ReturnsValidationFailure()
    {
        var (item, variant, addon) = CreateCatalog();
        var customer1 = CreateCustomer("Alice", "0901111111");
        var customer2 = CreateCustomer("Bob", "0902222222");
        var address2 = customer2.AddAddress(EntityId.New(), "456 Oak Ave");
        var orderId = EntityId.New();

        var reader = new InMemoryCustomerDeliveryAddressReader(customer1, customer2);
        var factory = new OrderDraftFactory(reader);

        var delivery = Delivery.CreateFromCustomerAddress(EntityId.New(), orderId, "Alice", "0901111111", address2.Id, "456 Oak Ave");
        var request = new CreateOrderDraftRequest(
            orderId,
            "ORD-DEL-009",
            "delivery",
            EntityId.New(),
            Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])],
            CustomerId: customer1.Id,
            Delivery: delivery);

        var result = factory.Create(request);

        Assert.True(result.IsFailure);
        Assert.Contains("cross customer boundaries", result.Error!.Description);
    }

    [Fact]
    public void CustomerDeliveryAddressReader_ReturnsExpectedReadModels()
    {
        var customer = CreateCustomer("Alice Smith", "0901234567");
        var addr1 = customer.AddAddress(EntityId.New(), "123 Main St", "Home", isDefault: true);
        var addr2 = customer.AddAddress(EntityId.New(), "456 Office Rd", "Work", isDefault: false);
        var addr3 = customer.AddAddress(EntityId.New(), "789 Deactivated St", "Old", isDefault: false);
        customer.DeactivateAddress(addr3.Id);

        var reader = new InMemoryCustomerDeliveryAddressReader(customer);

        var activeAddresses = reader.GetActiveAddresses(customer.Id);
        Assert.Equal(2, activeAddresses.Count);
        Assert.Contains(activeAddresses, a => a.AddressId == addr1.Id && a.IsDefault);
        Assert.Contains(activeAddresses, a => a.AddressId == addr2.Id && !a.IsDefault);

        var defaultAddress = reader.GetDefaultAddress(customer.Id);
        Assert.NotNull(defaultAddress);
        Assert.Equal(addr1.Id, defaultAddress.AddressId);

        var queriedAddr2 = reader.GetAddress(customer.Id, addr2.Id);
        Assert.NotNull(queriedAddr2);
        Assert.Equal("456 Office Rd", queriedAddr2.AddressLine);

        // Enforces customer boundary
        var unknownCustomer = reader.GetAddress(EntityId.New(), addr1.Id);
        Assert.Null(unknownCustomer);
    }

    private static Customer CreateCustomer(string name, string phone) =>
        new(EntityId.New(), name, phone, Now, Now);

    private static (MenuItem Item, SizeVariant Variant, Addon Addon) CreateCatalog()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        var item = new MenuItem(EntityId.New(), category, "Latte", Now, Now);
        return (item, new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m)), new Addon(EntityId.New(), "Extra shot", new Money(10000m)));
    }
}
