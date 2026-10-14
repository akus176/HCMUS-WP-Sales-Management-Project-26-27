namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;

public sealed class FakeOrderConfirmationRepository : IOrderConfirmationRepository
{
    public Dictionary<EntityId, Order> Orders { get; } = [];

    public Dictionary<EntityId, DiningTable> Tables { get; } = [];

    public Dictionary<EntityId, MenuItem> MenuItems { get; } = [];

    public Dictionary<EntityId, Category> Categories { get; } = [];

    public Dictionary<EntityId, SizeVariant> SizeVariants { get; } = [];

    public Dictionary<EntityId, Addon> Addons { get; } = [];

    public List<MenuItemAddon> CompatibilityLinks { get; } = [];

    public Dictionary<EntityId, IReadOnlyList<RecipeIngredient>> Recipes { get; } = [];

    public Dictionary<EntityId, Ingredient> Ingredients { get; } = [];

    public List<StockMovement> StockMovements { get; } = [];

    public int OrderLockCount { get; private set; }

    public int IngredientLockCount { get; private set; }

    public int TableLockCount { get; private set; }

    public Task<Order?> LoadOrderForConfirmationAsync(EntityId orderId, CancellationToken cancellationToken = default)
    {
        OrderLockCount++;
        return Task.FromResult(Orders.GetValueOrDefault(orderId));
    }

    public Task<DiningTable?> LoadTableForConfirmationAsync(EntityId tableId, CancellationToken cancellationToken = default)
    {
        TableLockCount++;
        return Task.FromResult(Tables.GetValueOrDefault(tableId));
    }

    public Task<OrderCatalogSnapshot> LoadCatalogForConfirmationAsync(
        IReadOnlyCollection<EntityId> menuItemIds,
        IReadOnlyCollection<EntityId> sizeVariantIds,
        IReadOnlyCollection<EntityId> addonIds,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new OrderCatalogSnapshot(
            MenuItems.Where(kvp => menuItemIds.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Categories.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            SizeVariants.Where(kvp => sizeVariantIds.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Addons.Where(kvp => addonIds.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CompatibilityLinks);
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<RecipeIngredient>> LoadRecipesAsync(
        IReadOnlyCollection<EntityId> sizeVariantIds,
        CancellationToken cancellationToken = default)
    {
        var lines = Recipes.Where(kvp => sizeVariantIds.Contains(kvp.Key))
            .SelectMany(kvp => kvp.Value).ToArray();
        return Task.FromResult<IReadOnlyList<RecipeIngredient>>(lines);
    }

    public Task<IReadOnlyList<Ingredient>> LoadIngredientsForConsumptionAsync(
        IReadOnlyCollection<EntityId> ingredientIds,
        CancellationToken cancellationToken = default)
    {
        IngredientLockCount++;
        var locked = Ingredients.Where(kvp => ingredientIds.Contains(kvp.Key))
            .Select(kvp => kvp.Value).ToArray();
        return Task.FromResult<IReadOnlyList<Ingredient>>(locked);
    }

    public void AddStockMovement(StockMovement movement) => StockMovements.Add(movement);
}
