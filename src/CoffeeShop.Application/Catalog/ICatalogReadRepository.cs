namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Domain.Common;

public interface ICatalogReadRepository
{
    Task<IReadOnlyList<AddonSummary>> ListAddonsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecipeIngredientOption>> ListActiveRecipeIngredientsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategorySummary>> SearchCategoriesAsync(
        CategorySearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuItemSummary>> SearchMenuItemsAsync(
        MenuItemSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<MenuItemDetail?> GetMenuItemDetailAsync(
        EntityId menuItemId,
        CancellationToken cancellationToken = default);

    Task<RecipeDetail?> GetRecipeAsync(
        EntityId sizeVariantId,
        CancellationToken cancellationToken = default);
}
