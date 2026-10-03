namespace CoffeeShop.Application.Tests.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public class OrderDraftFactoryTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData("takeaway")]
    [InlineData("dine_in")]
    [InlineData("delivery")]
    public void Create_ValidChannel_CapturesImmutableSnapshotsAndDerivedTotals(string channel)
    {
        var (item, variant, addon) = CreateCatalog();
        var orderId = EntityId.New();
        var delivery = channel == "delivery" ? new Delivery(EntityId.New(), orderId, "Ada", "0900000000", "1 Main Street") : null;
        var request = new CreateOrderDraftRequest(orderId, "ORD-001", channel, EntityId.New(), Now,
            [new OrderLineSelection(item, variant, 2, [addon], [new MenuItemAddon(item.Id, addon.Id)])], TableId: channel == "dine_in" ? EntityId.New() : null, Delivery: delivery);

        var result = new OrderDraftFactory().Create(request);

        Assert.True(result.IsSuccess);
        var order = result.Value;
        var line = Assert.Single(order.Lines);
        Assert.Equal("Latte", line.ItemNameSnapshot);
        Assert.Equal("Large", line.SizeNameSnapshot);
        Assert.Equal(new Money(45000m), line.UnitPriceSnapshot);
        Assert.Equal(new Money(110000m), line.LineTotal);
        Assert.Equal(new Money(110000m), order.Subtotal);
        Assert.Equal(order.Subtotal, order.Total);
        var snapshotAddon = Assert.Single(line.Addons);
        Assert.Equal("Extra shot", snapshotAddon.AddonNameSnapshot);
        Assert.Equal(new Money(10000m), snapshotAddon.AddonPriceSnapshot);
    }

    [Theory]
    [InlineData("dine_in")]
    [InlineData("delivery")]
    [InlineData("unsupported")]
    public void Create_InvalidChannelRequirements_ReturnsSharedValidationError(string channel)
    {
        var (item, variant, addon) = CreateCatalog();
        var request = new CreateOrderDraftRequest(EntityId.New(), "ORD-002", channel, EntityId.New(), Now,
            [new OrderLineSelection(item, variant, 1, [addon], [new MenuItemAddon(item.Id, addon.Id)])]);

        var result = new OrderDraftFactory().Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("orders.invalid", result.Error.Code);
    }

    [Fact]
    public void Create_UsesSelectedSizeAndPreservesSnapshotAfterPriceAndAvailabilityChanges()
    {
        var (item, large, _) = CreateCatalog();
        var small = new SizeVariant(EntityId.New(), item.Id, "Small", new Money(30000m));
        var request = new CreateOrderDraftRequest(EntityId.New(), "ORD-004", "takeaway", EntityId.New(), Now,
            [new OrderLineSelection(item, small, 1, []), new OrderLineSelection(item, large, 2, [])]);
        var factory = new OrderDraftFactory();

        var order = factory.Create(request).Value;
        large.Update("Extra large", new Money(50000m));
        large.Deactivate();

        Assert.Equal([new Money(30000m), new Money(45000m)], order.Lines.Select(line => line.UnitPriceSnapshot));
        Assert.Equal(["Small", "Large"], order.Lines.Select(line => line.SizeNameSnapshot));
        Assert.Equal(new Money(120000m), order.Subtotal);
        Assert.Equal(order.Subtotal, order.Total);
        Assert.Equal(ErrorType.Validation, factory.Create(request).Error!.Type);
    }

    [Fact]
    public void Create_InactiveCatalogSelection_ReturnsSharedValidationError()
    {
        var (item, variant, addon) = CreateCatalog();
        var inactive = new Addon(addon.Id, addon.Name, addon.Price, isActive: false);
        var request = new CreateOrderDraftRequest(EntityId.New(), "ORD-003", "takeaway", EntityId.New(), Now,
            [new OrderLineSelection(item, variant, 1, [inactive], [new MenuItemAddon(item.Id, inactive.Id)])]);

        var result = new OrderDraftFactory().Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Theory]
    [InlineData("incompatible")]
    [InlineData("foreign_item")]
    [InlineData("duplicate")]
    [InlineData("missing_compatibility")]
    [InlineData("zero_quantity")]
    [InlineData("negative_quantity")]
    public void Create_InvalidAddonSelection_ReturnsValidationWithoutCreatingOrder(string reason)
    {
        var (item, variant, addon) = CreateCatalog();
        MenuItemAddon[] links = reason == "incompatible" ? [] : [new(reason == "foreign_item" ? EntityId.New() : item.Id, addon.Id)];
        var quantity = reason == "zero_quantity" ? 0 : reason == "negative_quantity" ? -1 : 1;
        var selection = new OrderLineSelection(item, variant, quantity, reason == "duplicate" ? [addon, addon] : [addon],
            reason == "missing_compatibility" ? null : links);
        var request = new CreateOrderDraftRequest(EntityId.New(), "ORD-005", "takeaway", EntityId.New(), Now, [selection]);

        var result = new OrderDraftFactory().Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("orders.invalid", result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Create_RechecksAddonAvailabilityAndCompatibilityAndPreservesExistingOrder()
    {
        var (item, variant, addon) = CreateCatalog();
        var links = new List<MenuItemAddon> { new(item.Id, addon.Id) };
        var request = new CreateOrderDraftRequest(EntityId.New(), "ORD-006", "takeaway", EntityId.New(), Now,
            [new OrderLineSelection(item, variant, 2, [addon], links)]);
        var factory = new OrderDraftFactory();
        var order = factory.Create(request).Value;
        addon.Update("Changed", new Money(20000m));
        addon.Deactivate();

        Assert.Equal(ErrorType.Validation, factory.Create(request).Error!.Type);
        addon.Activate();
        links.Clear();
        Assert.Equal(ErrorType.Validation, factory.Create(request).Error!.Type);
        Assert.Equal(new Money(110000m), order.Total);
        var snapshot = Assert.Single(Assert.Single(order.Lines).Addons);
        Assert.Equal("Extra shot", snapshot.AddonNameSnapshot);
        Assert.Equal(new Money(10000m), snapshot.AddonPriceSnapshot);
    }

    private static (MenuItem Item, SizeVariant Variant, Addon Addon) CreateCatalog()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        var item = new MenuItem(EntityId.New(), category, "Latte", Now, Now);
        return (item, new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m)), new Addon(EntityId.New(), "Extra shot", new Money(10000m)));
    }
}
