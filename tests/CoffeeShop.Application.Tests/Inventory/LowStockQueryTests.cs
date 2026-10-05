namespace CoffeeShop.Application.Tests.Inventory;

using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class LowStockQueryTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Find_ReturnsOnlyActiveIngredientsAtOrBelowThreshold()
    {
        var above = Create("Above", availableQuantity: 11m, reorderThreshold: 10m);
        var equal = Create("Equal", availableQuantity: 10m, reorderThreshold: 10m);
        var below = Create("Below", availableQuantity: 9m, reorderThreshold: 10m);
        var inactive = Create("Inactive", availableQuantity: 0m, reorderThreshold: 10m, isActive: false);

        var results = LowStockQuery.Find([above, equal, below, inactive]);

        Assert.Equal([below.Id, equal.Id], results.Select(result => result.Id));
        Assert.DoesNotContain(results, result => result.Id == inactive.Id);
    }

    [Fact]
    public void Find_OrdersResultsByNameThenIdentifierAndSupportsLimit()
    {
        var zulu = Create("Zulu", availableQuantity: 1m, reorderThreshold: 2m);
        var alpha = Create("alpha", availableQuantity: 1m, reorderThreshold: 2m);
        var bravo = Create("Bravo", availableQuantity: 1m, reorderThreshold: 2m);

        var results = LowStockQuery.Find([zulu, bravo, alpha], limit: 2);

        Assert.Equal([alpha.Id, bravo.Id], results.Select(result => result.Id));
        Assert.Equal(1m, results[0].AvailableQuantity);
        Assert.Equal(2m, results[0].ReorderThreshold);
    }

    [Fact]
    public void Find_RejectsNonPositiveLimit()
    {
        var ingredient = Create("Beans", availableQuantity: 1m, reorderThreshold: 2m);

        Assert.Throws<ArgumentOutOfRangeException>(() => LowStockQuery.Find([ingredient], limit: 0));
    }

    private static Ingredient Create(
        string name,
        decimal availableQuantity,
        decimal reorderThreshold,
        bool isActive = true) =>
        new(EntityId.New(), name, "g", Now, Now, availableQuantity, reorderThreshold, isActive);
}
