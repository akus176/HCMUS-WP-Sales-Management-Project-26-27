namespace CoffeeShop.Domain.Tests.Sales;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public class DeliveryTests
{
    private static UtcTimestamp Now => new(DateTimeOffset.UtcNow);

    [Fact]
    public void CreateOrderSpecific_WithValidData_InitializesCorrectly()
    {
        var id = EntityId.New();
        var orderId = EntityId.New();

        var delivery = Delivery.CreateOrderSpecific(id, orderId, " Alice Smith ", " 0901234567 ", " 123 Main St ");

        Assert.Equal(id, delivery.Id);
        Assert.Equal(orderId, delivery.OrderId);
        Assert.Equal("Alice Smith", delivery.RecipientName);
        Assert.Equal("0901234567", delivery.ContactNumber);
        Assert.Equal("123 Main St", delivery.AddressLine);
        Assert.Null(delivery.CustomerAddressId);
        Assert.True(delivery.IsOrderSpecific);
        Assert.False(delivery.IsReusableCustomerAddress);
        Assert.Equal("unassigned", delivery.Status);
    }

    [Fact]
    public void CreateFromCustomerAddress_WithValidData_InitializesCorrectly()
    {
        var id = EntityId.New();
        var orderId = EntityId.New();
        var addressId = EntityId.New();

        var delivery = Delivery.CreateFromCustomerAddress(id, orderId, "Bob Jones", "+1 (555) 123-4567", addressId, "456 Office Rd");

        Assert.Equal(id, delivery.Id);
        Assert.Equal(orderId, delivery.OrderId);
        Assert.Equal("Bob Jones", delivery.RecipientName);
        Assert.Equal("+1 (555) 123-4567", delivery.ContactNumber);
        Assert.Equal("456 Office Rd", delivery.AddressLine);
        Assert.Equal(addressId, delivery.CustomerAddressId);
        Assert.False(delivery.IsOrderSpecific);
        Assert.True(delivery.IsReusableCustomerAddress);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidRecipientName_ThrowsArgumentException(string invalidName)
    {
        Assert.Throws<ArgumentException>(() =>
            new Delivery(EntityId.New(), EntityId.New(), invalidName, "0901234567", "123 Main St"));
    }

    [Fact]
    public void Constructor_WithRecipientNameExceeding120Chars_ThrowsArgumentException()
    {
        var tooLong = new string('N', 121);
        Assert.Throws<ArgumentException>(() =>
            new Delivery(EntityId.New(), EntityId.New(), tooLong, "0901234567", "123 Main St"));
    }

    [Fact]
    public void Constructor_WithRecipientNameBoundary120Chars_Succeeds()
    {
        var exact120 = new string('N', 120);
        var delivery = new Delivery(EntityId.New(), EntityId.New(), exact120, "0901234567", "123 Main St");
        Assert.Equal(exact120, delivery.RecipientName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")] // too short (< 7 digits)
    [InlineData("not-a-phone")]
    public void Constructor_WithInvalidContactNumber_ThrowsArgumentException(string invalidPhone)
    {
        Assert.Throws<ArgumentException>(() =>
            new Delivery(EntityId.New(), EntityId.New(), "Alice", invalidPhone, "123 Main St"));
    }

    [Fact]
    public void Constructor_WithContactNumberExceeding30Chars_ThrowsArgumentException()
    {
        var tooLong = "+1234567890123456789012345678901";
        Assert.Throws<ArgumentException>(() =>
            new Delivery(EntityId.New(), EntityId.New(), "Alice", tooLong, "123 Main St"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAddressLine_ThrowsArgumentException(string invalidAddress)
    {
        Assert.Throws<ArgumentException>(() =>
            new Delivery(EntityId.New(), EntityId.New(), "Alice", "0901234567", invalidAddress));
    }

    [Fact]
    public void Constructor_WithAddressLineExceeding500Chars_ThrowsArgumentException()
    {
        var tooLong = new string('A', 501);
        Assert.Throws<ArgumentException>(() =>
            new Delivery(EntityId.New(), EntityId.New(), "Alice", "0901234567", tooLong));
    }

    [Fact]
    public void Order_TakeawayWithDeliveryDetails_ThrowsArgumentException()
    {
        var orderId = EntityId.New();
        var line = CreateOrderLine();
        var delivery = new Delivery(EntityId.New(), orderId, "Alice", "0901234567", "123 Main St");

        var ex = Assert.Throws<ArgumentException>(() =>
            new Order(orderId, "ORD-001", "takeaway", EntityId.New(), Now, [line], delivery: delivery));

        Assert.Contains("Only delivery orders can contain delivery details.", ex.Message);
    }

    [Fact]
    public void Order_DineInWithDeliveryDetails_ThrowsArgumentException()
    {
        var orderId = EntityId.New();
        var line = CreateOrderLine();
        var delivery = new Delivery(EntityId.New(), orderId, "Alice", "0901234567", "123 Main St");

        var ex = Assert.Throws<ArgumentException>(() =>
            new Order(orderId, "ORD-001", "dine_in", EntityId.New(), Now, [line], tableId: EntityId.New(), delivery: delivery));

        Assert.Contains("Only delivery orders can contain delivery details.", ex.Message);
    }

    [Fact]
    public void Order_DeliveryWithoutDeliveryDetails_ThrowsArgumentException()
    {
        var orderId = EntityId.New();
        var line = CreateOrderLine();

        var ex = Assert.Throws<ArgumentException>(() =>
            new Order(orderId, "ORD-001", "delivery", EntityId.New(), Now, [line], delivery: null));

        Assert.Contains("Delivery orders require delivery details.", ex.Message);
    }

    [Fact]
    public void Order_DeliveryReferencingCustomerAddressWithoutCustomer_ThrowsArgumentException()
    {
        var orderId = EntityId.New();
        var line = CreateOrderLine();
        var delivery = Delivery.CreateFromCustomerAddress(EntityId.New(), orderId, "Alice", "0901234567", EntityId.New(), "123 Main St");

        var ex = Assert.Throws<ArgumentException>(() =>
            new Order(orderId, "ORD-001", "delivery", EntityId.New(), Now, [line], customerId: null, delivery: delivery));

        Assert.Contains("attached customer", ex.Message);
    }

    [Fact]
    public void Order_DeliveryWithOrderSpecificAddressWithoutCustomer_SucceedsForWalkIn()
    {
        var orderId = EntityId.New();
        var line = CreateOrderLine();
        var delivery = Delivery.CreateOrderSpecific(EntityId.New(), orderId, "Walk In Recipient", "0901234567", "123 Street");

        var order = new Order(orderId, "ORD-001", "delivery", EntityId.New(), Now, [line], customerId: null, delivery: delivery);

        Assert.NotNull(order);
        Assert.Null(order.CustomerId);
        Assert.True(delivery.IsOrderSpecific);
    }

    [Fact]
    public void Order_DeliveryWithReusableAddressAndAttachedCustomer_Succeeds()
    {
        var orderId = EntityId.New();
        var customerId = EntityId.New();
        var addressId = EntityId.New();
        var line = CreateOrderLine();
        var delivery = Delivery.CreateFromCustomerAddress(EntityId.New(), orderId, "Alice", "0901234567", addressId, "123 Main St");

        var order = new Order(orderId, "ORD-001", "delivery", EntityId.New(), Now, [line], customerId: customerId, delivery: delivery);

        Assert.NotNull(order);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(addressId, delivery.CustomerAddressId);
    }

    [Fact]
    public void Fulfillment_RecordsActorAuditedTransitions()
    {
        var delivery = Delivery.CreateOrderSpecific(EntityId.New(), EntityId.New(), "Alice", "0901234567", "1 Main St");
        var actor = EntityId.New();
        var staff = EntityId.New();

        delivery.Assign(staff, actor, Now);
        delivery.StartDelivery(actor, Now);
        delivery.Complete(actor, Now);

        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(staff, delivery.AssignedStaffId);
        Assert.Equal(3, delivery.StatusHistory.Count);
        Assert.All(delivery.StatusHistory, history => Assert.Equal(actor, history.ActorId));
    }

    [Fact]
    public void Fulfillment_RejectsInvalidOrRepeatedTransitions()
    {
        var delivery = Delivery.CreateOrderSpecific(EntityId.New(), EntityId.New(), "Alice", "0901234567", "1 Main St");
        Assert.Throws<InvalidOperationException>(() => delivery.StartDelivery(EntityId.New(), Now));
        delivery.Assign(EntityId.New(), EntityId.New(), Now);
        Assert.Throws<InvalidOperationException>(() => delivery.Assign(EntityId.New(), EntityId.New(), Now));
    }

    private static OrderLine CreateOrderLine()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        var item = new MenuItem(EntityId.New(), category, "Americano", Now, Now);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Regular", new Money(30000m));
        return new OrderLine(EntityId.New(), item, variant, 1, []);
    }
}
