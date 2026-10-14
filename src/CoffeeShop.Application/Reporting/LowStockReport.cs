namespace CoffeeShop.Application.Reporting;

using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Inventory;

/// <summary>
/// Builds the current low-stock report from resolved ingredients.
/// The report intentionally reuses the inventory low-stock query so both
/// surfaces always agree on the boundary rule, ordering, and shape.
/// </summary>
public static class LowStockReport
{
    public static IReadOnlyList<LowStockIngredient> Build(
        IEnumerable<Ingredient> ingredients,
        int? limit = null) =>
        LowStockQuery.Find(ingredients, limit);
}
