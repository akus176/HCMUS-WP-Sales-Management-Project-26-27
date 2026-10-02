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
            [new OrderLineSelection(item, variant, 2, [addon])], TableId: channel == "dine_in" ? EntityId.New() : null, Delivery: delivery);

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
            [new OrderLineSelection(item, variant, 1, [addon])]);

        var result = new OrderDraftFactory().Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("orders.invalid", result.Error.Code);
    }

    [Fact]
    public void Create_InactiveCatalogSelection_ReturnsSharedValidationError()
    {
        var (item, variant, addon) = CreateCatalog();
        var inactive = new Addon(addon.Id, addon.Name, addon.Price, isActive: false);
        var request = new CreateOrderDraftRequest(EntityId.New(), "ORD-003", "takeaway", EntityId.New(), Now,
            [new OrderLineSelection(item, variant, 1, [inactive])]);

        var result = new OrderDraftFactory().Create(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    private static (MenuItem Item, SizeVariant Variant, Addon Addon) CreateCatalog()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        var item = new MenuItem(EntityId.New(), category, "Latte", Now, Now);
        return (item, new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m)), new Addon(EntityId.New(), "Extra shot", new Money(10000m)));
    }
}
