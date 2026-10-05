namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Domain.Common;

/// <summary>
/// Stable, persistence-independent data returned by the low-stock query.
/// </summary>
public sealed record LowStockIngredient(
    EntityId Id,
    string Name,
    string BaseUnit,
    decimal AvailableQuantity,
    decimal ReorderThreshold);
