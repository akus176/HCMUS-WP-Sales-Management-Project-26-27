namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public sealed class FakeCatalogRepository : ICatalogRepository, ICatalogReadRepository
{
    public Dictionary<EntityId, Category> Categories { get; } = [];

    public Dictionary<EntityId, MenuItem> MenuItems { get; } = [];

    public Dictionary<EntityId, SizeVariant> SizeVariants { get; } = [];

    public Dictionary<EntityId, Addon> Addons { get; } = [];

    public Dictionary<EntityId, Ingredient> Ingredients { get; } = [];

    public List<MenuItemAddon> Compatibility { get; } = [];

    public Dictionary<EntityId, IReadOnlyCollection<RecipeIngredient>> Recipes { get; } = [];

    public IReadOnlyList<CategorySummary> CategorySearchResults { get; set; } = [];

    public IReadOnlyList<MenuItemSummary> SearchResults { get; set; } = [];

    public MenuItemDetail? DetailResult { get; set; }

    public RecipeDetail? RecipeDetailResult { get; set; }

    public CategorySearchCriteria? LastCategorySearchCriteria { get; private set; }

    public MenuItemSearchCriteria? LastSearchCriteria { get; private set; }

    public Task<Category?> FindCategoryAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Categories.GetValueOrDefault(id));

    public Task<MenuItem?> FindMenuItemAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(MenuItems.GetValueOrDefault(id));

    public Task<SizeVariant?> FindSizeVariantAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(SizeVariants.GetValueOrDefault(id));

    public Task<Addon?> FindAddonAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Addons.GetValueOrDefault(id));

    public Task<Ingredient?> FindIngredientAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Ingredients.GetValueOrDefault(id));

    public Task<IReadOnlyList<Ingredient>> ListIngredientsForRecipeUpdateAsync(
        IReadOnlyCollection<EntityId> ingredientIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Ingredient>>(Ingredients.Values
            .Where(ingredient => ingredientIds.Contains(ingredient.Id)).ToList());

    public Task<IReadOnlyList<SizeVariant>> ListSizeVariantsAsync(
        EntityId menuItemId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SizeVariant>>(SizeVariants.Values
            .Where(variant => variant.MenuItemId == menuItemId)
            .ToList()
            .AsReadOnly());

    public void AddCategory(Category category) => Categories.Add(category.Id, category);

    public void AddMenuItem(MenuItem menuItem) => MenuItems.Add(menuItem.Id, menuItem);

    public void AddSizeVariant(SizeVariant sizeVariant) => SizeVariants.Add(sizeVariant.Id, sizeVariant);

    public void AddAddon(Addon addon) => Addons.Add(addon.Id, addon);

    public Task ReplaceCompatibleAddonsAsync(
        EntityId menuItemId,
        IReadOnlyCollection<MenuItemAddon> compatibility,
        CancellationToken cancellationToken = default)
    {
        Compatibility.RemoveAll(link => link.MenuItemId == menuItemId);
        Compatibility.AddRange(compatibility);
        return Task.CompletedTask;
    }

    public Task ReplaceRecipeAsync(
        EntityId sizeVariantId,
        IReadOnlyCollection<RecipeIngredient> recipe,
        CancellationToken cancellationToken = default)
    {
        Recipes[sizeVariantId] = recipe;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CategorySummary>> SearchCategoriesAsync(
        CategorySearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        LastCategorySearchCriteria = criteria;
        return Task.FromResult(CategorySearchResults);
    }

    public Task<IReadOnlyList<MenuItemSummary>> SearchMenuItemsAsync(
        MenuItemSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        LastSearchCriteria = criteria;
        return Task.FromResult(SearchResults);
    }

    public Task<MenuItemDetail?> GetMenuItemDetailAsync(
        EntityId menuItemId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DetailResult?.Id == menuItemId ? DetailResult : null);

    public Task<RecipeDetail?> GetRecipeAsync(
        EntityId sizeVariantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RecipeDetailResult?.SizeVariantId == sizeVariantId ? RecipeDetailResult : null);
}
