namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Domain.Inventory;

/// <summary>
/// Builds the current low-stock read model from resolved ingredients.
/// Loading and persistence are intentionally owned by the caller.
/// </summary>
public static class LowStockQuery
{
    public static IReadOnlyList<LowStockIngredient> Find(
        IEnumerable<Ingredient> ingredients,
        int? limit = null)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        if (limit is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var query = ingredients
            .Where(ingredient => ingredient.IsActive && ingredient.AvailableQuantity <= ingredient.ReorderThreshold)
            .OrderBy(ingredient => ingredient.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(ingredient => ingredient.Id.Value)
            .Select(ingredient => new LowStockIngredient(
                ingredient.Id,
                ingredient.Name,
                ingredient.BaseUnit,
                ingredient.AvailableQuantity,
                ingredient.ReorderThreshold));

        if (limit is not null)
        {
            query = query.Take(limit.Value);
        }

        return query.ToList().AsReadOnly();
    }
}
