namespace CoffeeShop.Application.Tests.Catalog;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;

public sealed class CatalogQueryHandlerTests
{
    [Fact]
    public async Task SearchCategories_NormalizesCriteriaAndReturnsReadModels()
    {
        var repository = new FakeCatalogRepository();
        var category = new CategorySummary(EntityId.New(), "Coffee", 1, true);
        repository.CategorySearchResults = [category];
        var handler = new CatalogQueryHandler(repository);

        var result = await handler.SearchCategoriesAsync(new CategorySearchCriteria(
            Search: " coffee ",
            IsActive: true,
            Limit: 25));

        Assert.True(result.IsSuccess);
        Assert.Same(category, Assert.Single(result.Value));
        Assert.NotNull(repository.LastCategorySearchCriteria);
        Assert.Equal("coffee", repository.LastCategorySearchCriteria.Search);
        Assert.True(repository.LastCategorySearchCriteria.IsActive);
        Assert.Equal(25, repository.LastCategorySearchCriteria.Limit);
    }

    [Fact]
    public async Task SearchCategories_WithInvalidCriteria_ReturnsValidation()
    {
        var repository = new FakeCatalogRepository();
        var handler = new CatalogQueryHandler(repository);

        var result = await handler.SearchCategoriesAsync(new CategorySearchCriteria(Limit: 0));

        AssertError(result, ErrorType.Validation, "catalog.invalid_limit");
        Assert.Null(repository.LastCategorySearchCriteria);
    }

    [Fact]
    public async Task SearchMenuItems_NormalizesCriteriaAndReturnsReadModels()
    {
        var repository = new FakeCatalogRepository();
        var categoryId = EntityId.New();
        var item = CreateSummary(categoryId);
        repository.SearchResults = [item];
        var handler = new CatalogQueryHandler(repository);

        var result = await handler.SearchMenuItemsAsync(new MenuItemSearchCriteria(
            Search: " latte ",
            CategoryId: categoryId,
            IsActive: true,
            Limit: 25));

        Assert.True(result.IsSuccess);
        Assert.Same(item, Assert.Single(result.Value));
        Assert.NotNull(repository.LastSearchCriteria);
        Assert.Equal("latte", repository.LastSearchCriteria.Search);
        Assert.Equal(categoryId, repository.LastSearchCriteria.CategoryId);
        Assert.True(repository.LastSearchCriteria.IsActive);
        Assert.Equal(25, repository.LastSearchCriteria.Limit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task SearchMenuItems_WithInvalidLimit_ReturnsValidation(int limit)
    {
        var repository = new FakeCatalogRepository();
        var handler = new CatalogQueryHandler(repository);

        var result = await handler.SearchMenuItemsAsync(new MenuItemSearchCriteria(Limit: limit));

        AssertError(result, ErrorType.Validation, "catalog.invalid_limit");
        Assert.Null(repository.LastSearchCriteria);
    }

    [Fact]
    public async Task SearchMenuItems_WithLongSearch_ReturnsValidation()
    {
        var repository = new FakeCatalogRepository();
        var handler = new CatalogQueryHandler(repository);

        var result = await handler.SearchMenuItemsAsync(new MenuItemSearchCriteria(new string('a', 121)));

        AssertError(result, ErrorType.Validation, "catalog.search_too_long");
        Assert.Null(repository.LastSearchCriteria);
    }

    [Fact]
    public async Task GetMenuItemDetail_WithExistingItem_ReturnsFullDetail()
    {
        var repository = new FakeCatalogRepository();
        var id = EntityId.New();
        repository.DetailResult = new MenuItemDetail(
            id,
            EntityId.New(),
            "Coffee",
            "Latte",
            "Milk coffee",
            true,
            [new SizeVariantDetail(
                EntityId.New(),
                "Large",
                45000m,
                true,
                [new RecipeLineDetail(EntityId.New(), EntityId.New(), "Coffee beans", 18m, "g")])],
            [new AddonSummary(EntityId.New(), "Pearl", 8000m, true)]);
        var handler = new CatalogQueryHandler(repository);

        var result = await handler.GetMenuItemDetailAsync(id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Latte", result.Value.Name);
        Assert.Single(result.Value.SizeVariants);
        Assert.Single(result.Value.SizeVariants[0].Recipe);
        Assert.Single(result.Value.CompatibleAddons);
    }

    [Fact]
    public async Task GetMenuItemDetail_WithMissingItem_ReturnsNotFound()
    {
        var handler = new CatalogQueryHandler(new FakeCatalogRepository());

        var result = await handler.GetMenuItemDetailAsync(EntityId.New());

        AssertError(result, ErrorType.NotFound, "catalog.menu_item_not_found");
    }

    [Fact]
    public async Task GetMenuItemDetail_WithMissingIdentifier_ReturnsValidation()
    {
        var handler = new CatalogQueryHandler(new FakeCatalogRepository());

        var result = await handler.GetMenuItemDetailAsync(null!);

        AssertError(result, ErrorType.Validation, "catalog.invalid_input");
    }

    [Fact]
    public async Task GetRecipe_WithExistingVariant_ReturnsSizeSpecificRecipe()
    {
        var repository = new FakeCatalogRepository();
        var sizeVariantId = EntityId.New();
        repository.RecipeDetailResult = new RecipeDetail(
            sizeVariantId,
            EntityId.New(),
            "Large",
            [new RecipeLineDetail(EntityId.New(), EntityId.New(), "Coffee beans", 18m, "g")]);
        var handler = new CatalogQueryHandler(repository);

        var result = await handler.GetRecipeAsync(sizeVariantId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Large", result.Value.SizeName);
        Assert.Single(result.Value.Lines);
    }

    [Fact]
    public async Task GetRecipe_WithMissingVariant_ReturnsNotFound()
    {
        var handler = new CatalogQueryHandler(new FakeCatalogRepository());

        var result = await handler.GetRecipeAsync(EntityId.New());

        AssertError(result, ErrorType.NotFound, "catalog.variant_not_found");
    }

    [Fact]
    public async Task GetRecipe_WithMissingIdentifier_ReturnsValidation()
    {
        var handler = new CatalogQueryHandler(new FakeCatalogRepository());

        var result = await handler.GetRecipeAsync(null!);

        AssertError(result, ErrorType.Validation, "catalog.invalid_input");
    }

    private static MenuItemSummary CreateSummary(EntityId categoryId) => new(
        EntityId.New(),
        categoryId,
        "Coffee",
        "Latte",
        "Milk coffee",
        true,
        [new SizeVariantSummary(EntityId.New(), "Large", 45000m, true)],
        [new AddonSummary(EntityId.New(), "Pearl", 8000m, true)]);

    private static void AssertError(Result result, ErrorType type, string code)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(type, result.Error.Type);
        Assert.Equal(code, result.Error.Code);
    }
}
