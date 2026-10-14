namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;

/// <summary>
/// Persistence contract for the atomic order-confirmation transaction.
/// Implementations must run inside the caller's transaction and take
/// row-level locks in a deterministic order so concurrent confirmations
/// cannot oversell the same ingredient.
/// </summary>
public interface IOrderConfirmationRepository
{
    /// <summary>Loads the order with lines and add-ons after locking its row.</summary>
    Task<Order?> LoadOrderForConfirmationAsync(EntityId orderId, CancellationToken cancellationToken = default);

    /// <summary>Loads the dining table after locking its row, or null when missing.</summary>
    Task<DiningTable?> LoadTableForConfirmationAsync(EntityId tableId, CancellationToken cancellationToken = default);

    /// <summary>Loads current menu-item, category, size-variant, add-on definitions and compatibility links for revalidation.</summary>
    Task<OrderCatalogSnapshot> LoadCatalogForConfirmationAsync(
        IReadOnlyCollection<EntityId> menuItemIds,
        IReadOnlyCollection<EntityId> sizeVariantIds,
        IReadOnlyCollection<EntityId> addonIds,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the size-specific recipe lines for the given variants.</summary>
    Task<IReadOnlyList<RecipeIngredient>> LoadRecipesAsync(
        IReadOnlyCollection<EntityId> sizeVariantIds,
        CancellationToken cancellationToken = default);

    /// <summary>Loads ingredients after locking their rows in deterministic id order.</summary>
    Task<IReadOnlyList<Ingredient>> LoadIngredientsForConsumptionAsync(
        IReadOnlyCollection<EntityId> ingredientIds,
        CancellationToken cancellationToken = default);

    void AddStockMovement(StockMovement movement);
}

/// <summary>Resolved current catalog definitions used to revalidate a draft order at confirmation.</summary>
public sealed record OrderCatalogSnapshot(
    IReadOnlyDictionary<EntityId, MenuItem> MenuItems,
    IReadOnlyDictionary<EntityId, Category> Categories,
    IReadOnlyDictionary<EntityId, SizeVariant> SizeVariants,
    IReadOnlyDictionary<EntityId, Addon> Addons,
    IReadOnlyList<MenuItemAddon> CompatibilityLinks);
