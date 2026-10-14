namespace CoffeeShop.Application.Tests.Reporting;

using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class LowStockReportTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 14, 1, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Build_MatchesTheInventoryLowStockQueryExactly()
    {
        var above = Create("Above", availableQuantity: 11m, reorderThreshold: 10m);
        var equal = Create("Equal", availableQuantity: 10m, reorderThreshold: 10m);
        var below = Create("Below", availableQuantity: 9m, reorderThreshold: 10m);
        var inactive = Create("Inactive", availableQuantity: 0m, reorderThreshold: 10m, isActive: false);
        var ingredients = new[] { above, equal, below, inactive };

        var report = LowStockReport.Build(ingredients);
        var inventory = LowStockQuery.Find(ingredients);

        Assert.Equal(inventory, report);
        Assert.Equal([below.Id, equal.Id], report.Select(row => row.Id));
        Assert.DoesNotContain(report, row => row.Id == inactive.Id);
    }

    [Fact]
    public void Build_IdentifiesIngredientBalanceThresholdAndUnit()
    {
        var beans = Create("Beans", availableQuantity: 1.5m, reorderThreshold: 2m);

        var report = LowStockReport.Build([beans]);

        var row = Assert.Single(report);
        Assert.Equal(beans.Id, row.Id);
        Assert.Equal("Beans", row.Name);
        Assert.Equal("g", row.BaseUnit);
        Assert.Equal(1.5m, row.AvailableQuantity);
        Assert.Equal(2m, row.ReorderThreshold);
    }

    [Fact]
    public void Build_SupportsLimitAndStableOrdering()
    {
        var zulu = Create("Zulu", availableQuantity: 1m, reorderThreshold: 2m);
        var alpha = Create("alpha", availableQuantity: 1m, reorderThreshold: 2m);
        var bravo = Create("Bravo", availableQuantity: 1m, reorderThreshold: 2m);

        var report = LowStockReport.Build([zulu, bravo, alpha], limit: 2);

        Assert.Equal([alpha.Id, bravo.Id], report.Select(row => row.Id));
    }

    [Fact]
    public void Build_RejectsNonPositiveLimit()
    {
        var ingredient = Create("Beans", availableQuantity: 1m, reorderThreshold: 2m);

        Assert.Throws<ArgumentOutOfRangeException>(() => LowStockReport.Build([ingredient], limit: 0));
    }

    private static Ingredient Create(
        string name,
        decimal availableQuantity,
        decimal reorderThreshold,
        bool isActive = true) =>
        new(EntityId.New(), name, "g", Now, Now, availableQuantity, reorderThreshold, isActive);
}
