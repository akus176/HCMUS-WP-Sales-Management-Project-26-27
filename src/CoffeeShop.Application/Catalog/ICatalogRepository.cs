namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public interface ICatalogRepository
{
    Task<Category?> FindCategoryAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<MenuItem?> FindMenuItemAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<SizeVariant?> FindSizeVariantAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<Addon?> FindAddonAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<Ingredient?> FindIngredientAsync(EntityId id, CancellationToken cancellationToken = default);

    // Load fresh definitions and prevent updates until the caller's transaction ends.
    Task<IReadOnlyList<Ingredient>> ListIngredientsForRecipeUpdateAsync(
        IReadOnlyCollection<EntityId> ingredientIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SizeVariant>> ListSizeVariantsAsync(
        EntityId menuItemId,
        CancellationToken cancellationToken = default);

    void AddCategory(Category category);

    void AddMenuItem(MenuItem menuItem);

    void AddSizeVariant(SizeVariant sizeVariant);

    void AddAddon(Addon addon);

    Task ReplaceCompatibleAddonsAsync(
        EntityId menuItemId,
        IReadOnlyCollection<MenuItemAddon> compatibility,
        CancellationToken cancellationToken = default);

    Task ReplaceRecipeAsync(
        EntityId sizeVariantId,
        IReadOnlyCollection<RecipeIngredient> recipe,
        CancellationToken cancellationToken = default);
}
