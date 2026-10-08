namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;

public sealed class CatalogQueryHandler
{
    private const int MaximumSearchLength = 120;
    private const int MaximumLimit = 200;
    private readonly ICatalogReadRepository repository;

    public CatalogQueryHandler(ICatalogReadRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Result<IReadOnlyList<AddonSummary>>> ListAddonsAsync(CancellationToken cancellationToken = default) =>
        Result<IReadOnlyList<AddonSummary>>.Success(await repository.ListAddonsAsync(cancellationToken));

    public async Task<Result<IReadOnlyList<RecipeIngredientOption>>> ListActiveRecipeIngredientsAsync(CancellationToken cancellationToken = default) =>
        Result<IReadOnlyList<RecipeIngredientOption>>.Success(await repository.ListActiveRecipeIngredientsAsync(cancellationToken));

    public async Task<Result<IReadOnlyList<CategorySummary>>> SearchCategoriesAsync(
        CategorySearchCriteria? criteria = null,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new CategorySearchCriteria();
        var search = criteria.Search?.Trim();
        var validation = ValidateSearch(search, criteria.Limit);
        if (validation is not null)
        {
            return Result<IReadOnlyList<CategorySummary>>.Failure(validation);
        }

        var normalized = criteria with
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search
        };
        var categories = await repository.SearchCategoriesAsync(normalized, cancellationToken);
        return Result<IReadOnlyList<CategorySummary>>.Success(categories);
    }

    public async Task<Result<IReadOnlyList<MenuItemSummary>>> SearchMenuItemsAsync(
        MenuItemSearchCriteria? criteria = null,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new MenuItemSearchCriteria();
        var search = criteria.Search?.Trim();
        var validation = ValidateSearch(search, criteria.Limit);
        if (validation is not null)
        {
            return Result<IReadOnlyList<MenuItemSummary>>.Failure(validation);
        }

        var normalized = criteria with
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search
        };
        var items = await repository.SearchMenuItemsAsync(normalized, cancellationToken);
        return Result<IReadOnlyList<MenuItemSummary>>.Success(items);
    }

    public async Task<Result<MenuItemDetail>> GetMenuItemDetailAsync(
        EntityId menuItemId,
        CancellationToken cancellationToken = default)
    {
        if (menuItemId is null)
        {
            return Result<MenuItemDetail>.Failure(Error.Validation(
                "catalog.invalid_input",
                "A menu item identifier is required.",
                [new ErrorDetail("menuItemId", "Menu item identifier is required.")]));
        }

        var detail = await repository.GetMenuItemDetailAsync(menuItemId, cancellationToken);
        return detail is null
            ? Result<MenuItemDetail>.Failure(Error.NotFound(
                "catalog.menu_item_not_found",
                "The selected menu item was not found."))
            : Result<MenuItemDetail>.Success(detail);
    }

    public async Task<Result<RecipeDetail>> GetRecipeAsync(
        EntityId sizeVariantId,
        CancellationToken cancellationToken = default)
    {
        if (sizeVariantId is null)
        {
            return Result<RecipeDetail>.Failure(Error.Validation(
                "catalog.invalid_input",
                "A size variant identifier is required.",
                [new ErrorDetail("sizeVariantId", "Size variant identifier is required.")]));
        }

        var recipe = await repository.GetRecipeAsync(sizeVariantId, cancellationToken);
        return recipe is null
            ? Result<RecipeDetail>.Failure(Error.NotFound(
                "catalog.variant_not_found",
                "The selected size variant was not found."))
            : Result<RecipeDetail>.Success(recipe);
    }

    private static Error? ValidateSearch(string? search, int limit)
    {
        if (search?.Length > MaximumSearchLength)
        {
            return Error.Validation(
                "catalog.search_too_long",
                $"Search text cannot exceed {MaximumSearchLength} characters.",
                [new ErrorDetail("search", "Search text is too long.")]);
        }

        return limit is < 1 or > MaximumLimit
            ? Error.Validation(
                "catalog.invalid_limit",
                $"Search limit must be between 1 and {MaximumLimit}.",
                [new ErrorDetail("limit", "Search limit is outside the allowed range.")])
            : null;
    }
}
