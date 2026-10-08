using System.Globalization;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Tests;

public sealed class CatalogWorkspaceTests
{
    [Fact]
    public async Task Operator_CreatesEditsSearchesAndDeactivatesCompleteDrink()
    {
        var repository = new FakeCatalogRepository();
        var reads = new WorkspaceReads(repository);
        using var services = Services(repository, reads);
        var model = new CatalogWorkspace(services.GetRequiredService<IServiceScopeFactory>());
        await model.LoadAsync();
        Assert.True(model.IsEmpty);

        Assert.True(await model.SaveAsync(async (commands, _) => await commands.CreateCategoryAsync(new("Coffee")), "Category saved."));
        var category = Assert.Single(model.Categories);
        Assert.True(await model.SaveAsync(async (commands, _) => await commands.CreateMenuItemAsync(
            new(category.Id, "Latte", "Milk coffee", [new("Small", 25.50m)])), "Drink saved."));
        var item = Assert.Single(model.Items);
        await model.SelectAsync(item.Id);
        var small = Assert.Single(model.SelectedItem!.SizeVariants);
        Assert.True(await model.SaveAsync(async (commands, _) => await commands.CreateSizeVariantAsync(new(item.Id, "Large", 35.75m)), "Size saved."));
        var large = Assert.Single(model.SelectedItem.SizeVariants, variant => variant.SizeName == "Large");
        Assert.True(await model.SaveAsync(async (commands, _) => await commands.CreateAddonAsync(new("Cream", 5m)), "Add-on saved."));
        var addon = Assert.Single(model.Addons);
        Assert.True(await model.SaveAsync((commands, _) => commands.SetMenuItemAddonsAsync(new(item.Id, [addon.Id])), "Compatibility saved."));

        var clock = new TestClock();
        var beans = new Ingredient(EntityId.New(), "Beans", "g", clock.UtcNow, clock.UtcNow);
        repository.Ingredients.Add(beans.Id, beans);
        Assert.True(await model.SaveAsync((_, recipes) => recipes.ReplaceRecipeAsync(new(small.Id, [new(beans.Id, 18m, "g")])), "Recipe saved."));
        Assert.True(await model.SaveAsync((_, recipes) => recipes.ReplaceRecipeAsync(new(large.Id, [new(beans.Id, 0.025m, "kg")])), "Recipe saved."));
        Assert.Equal(18m, Assert.Single(model.SelectedItem.SizeVariants.Single(variant => variant.Id == small.Id).Recipe).Quantity);
        Assert.Equal(0.025m, Assert.Single(model.SelectedItem.SizeVariants.Single(variant => variant.Id == large.Id).Recipe).Quantity);
        Assert.Equal(addon.Id, Assert.Single(model.SelectedItem.CompatibleAddons).Id);

        Assert.True(await model.SaveAsync((commands, _) => commands.UpdateMenuItemAsync(new(item.Id, category.Id, "House latte", "Edited drink")), "Drink saved."));
        Assert.True(await model.SaveAsync((commands, _) => commands.UpdateSizeVariantAsync(new(item.Id, large.Id, "Big", 40m)), "Size saved."));
        Assert.True(await model.SaveAsync((commands, _) => commands.UpdateAddonAsync(new(addon.Id, "Whipped cream", 6m)), "Add-on saved."));
        Assert.True(await model.SaveAsync((commands, _) => commands.UpdateCategoryAsync(new(category.Id, "Espresso drinks")), "Category saved."));
        Assert.Equal("Espresso drinks", model.SelectedItem.CategoryName);
        Assert.Equal(40m, model.SelectedItem.SizeVariants.Single(variant => variant.Id == large.Id).Price);
        Assert.Equal(6m, Assert.Single(model.SelectedItem.CompatibleAddons).Price);

        model.Search = "  house  ";
        await model.LoadAsync();
        Assert.Equal("House latte", Assert.Single(model.Items).Name);
        model.Search = "no match";
        await model.LoadAsync();
        Assert.True(model.IsEmpty);
        model.Search = "";
        Assert.True(await model.SaveAsync((commands, _) => commands.DeactivateSizeVariantAsync(new(large.Id)), "Size deactivated."));
        Assert.True(await model.SaveAsync((commands, _) => commands.DeactivateAddonAsync(new(addon.Id)), "Add-on deactivated."));
        Assert.True(await model.SaveAsync((commands, _) => commands.DeactivateMenuItemAsync(new(item.Id)), "Drink deactivated."));
        Assert.True(model.IsEmpty);
        model.IncludeInactive = true;
        await model.LoadAsync();
        Assert.False(Assert.Single(model.Items).IsActive);
        Assert.False(model.SelectedItem.IsActive);
        Assert.False(model.SelectedItem.SizeVariants.Single(variant => variant.Id == large.Id).IsActive);
        Assert.False(Assert.Single(model.SelectedItem.CompatibleAddons).IsActive);
        Assert.True(await model.SaveAsync((commands, _) => commands.DeactivateCategoryAsync(new(category.Id)), "Category deactivated."));
        Assert.False(Assert.Single(model.Categories).IsActive);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task InvalidRecipe_ShowsEveryFieldErrorAndKeepsSavedRecipe()
    {
        var repository = new FakeCatalogRepository();
        using var services = Services(repository, new WorkspaceReads(repository));
        var model = new CatalogWorkspace(services.GetRequiredService<IServiceScopeFactory>());
        await model.SaveAsync(async (commands, _) => await commands.CreateCategoryAsync(new("Coffee")), "Saved.");
        await model.SaveAsync(async (commands, _) => await commands.CreateMenuItemAsync(new(model.Categories[0].Id, "Latte", null, [new("Small", 20m)])), "Saved.");
        await model.SelectAsync(model.Items[0].Id);
        var variant = model.SelectedItem!.SizeVariants[0];
        var clock = new TestClock();
        var beans = new Ingredient(EntityId.New(), "Beans", "g", clock.UtcNow, clock.UtcNow);
        repository.Ingredients.Add(beans.Id, beans);
        await model.SaveAsync((_, recipes) => recipes.ReplaceRecipeAsync(new(variant.Id, [new(beans.Id, 18m, "g")])), "Saved.");

        var saved = await model.SaveAsync((_, recipes) => recipes.ReplaceRecipeAsync(new(variant.Id,
            [new(beans.Id, 0m, "g"), new(EntityId.New(), 10m, "ml")])), "Saved.");

        Assert.False(saved);
        Assert.True(model.IsError);
        Assert.Contains("lines[0].quantity", model.Message);
        Assert.Contains("lines[1].ingredientId", model.Message);
        Assert.Equal(18m, Assert.Single(repository.Recipes[variant.Id]).Quantity);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task Loading_PreventsDuplicateActionsAndPublishesState()
    {
        var repository = new FakeCatalogRepository();
        using var services = Services(repository, new WorkspaceReads(repository));
        var model = new CatalogWorkspace(services.GetRequiredService<IServiceScopeFactory>());
        var gate = new TaskCompletionSource<Result>();
        var states = new List<bool>();
        model.PropertyChanged += (_, _) => states.Add(model.IsBusy);
        var saving = model.SaveAsync((_, _) => gate.Task, "Saved.");
        Assert.True(model.IsBusy);
        Assert.Equal("Working...", model.Message);
        var invoked = false;
        Assert.False(await model.SaveAsync((_, _) => { invoked = true; return Task.FromResult(Result.Success()); }, "Duplicate."));
        Assert.False(invoked);
        gate.SetResult(Result.Success());
        Assert.True(await saving);
        Assert.Equal(new[] { true, false }, states);
    }

    [Fact]
    public async Task ReadFailure_IsActionableAndDoesNotExposeConnectionSecrets()
    {
        var repository = new FakeCatalogRepository();
        var reads = new WorkspaceReads(repository) { Failure = new InvalidOperationException("Password=private") };
        using var services = Services(repository, reads);
        var model = new CatalogWorkspace(services.GetRequiredService<IServiceScopeFactory>());
        await model.LoadAsync();
        Assert.True(model.IsError);
        Assert.False(model.IsEmpty);
        Assert.False(model.IsBusy);
        Assert.Contains("reload", model.Message);
        Assert.DoesNotContain("private", model.Message);
        reads.Failure = null;
        await model.LoadAsync();
        Assert.True(model.IsEmpty);
        Assert.False(model.IsError);
    }

    [Fact]
    public async Task SuccessfulWrite_FailedRefreshReportsSavedAndDoesNotInviteDuplicate()
    {
        var repository = new FakeCatalogRepository();
        var reads = new WorkspaceReads(repository) { Failure = new InvalidOperationException("offline") };
        using var services = Services(repository, reads);
        var model = new CatalogWorkspace(services.GetRequiredService<IServiceScopeFactory>());
        Assert.True(await model.SaveAsync(async (commands, _) => await commands.CreateAddonAsync(new("Cream", 5m)), "Add-on saved."));
        Assert.Single(repository.Addons);
        Assert.True(model.IsError);
        Assert.Contains("Add-on saved. Refresh failed", model.Message);
        Assert.Contains("do not submit again", model.Message);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    public async Task ApplicationFailure_StaysVisibleWithoutSuccessOrRefresh(ErrorType type)
    {
        var repository = new FakeCatalogRepository();
        using var services = Services(repository, new WorkspaceReads(repository));
        var model = new CatalogWorkspace(services.GetRequiredService<IServiceScopeFactory>());
        var error = new Error("test.error", "Correct the input or reload.", type, [new("name", "Name is required.")]);
        Assert.False(await model.SaveAsync((_, _) => Task.FromResult(Result.Failure(error)), "Saved."));
        Assert.True(model.IsError);
        Assert.Contains(type.ToString(), model.Message);
        Assert.Contains("name: Name is required.", model.Message);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task MissingSelection_ClearsPreviouslyShownDrink()
    {
        var repository = new FakeCatalogRepository();
        using var services = Services(repository, new WorkspaceReads(repository));
        var model = new CatalogWorkspace(services.GetRequiredService<IServiceScopeFactory>());
        await model.SelectAsync(EntityId.New());
        Assert.Null(model.SelectedItem);
        Assert.True(model.IsError);
        Assert.Contains("NotFound", model.Message);
    }

    [Theory]
    [InlineData("en-US", "25.50", "25,50")]
    [InlineData("vi-VN", "25,50", "25.50")]
    public void DecimalInput_UsesCurrentCultureWithoutGrouping(string culture, string valid, string invalid)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal(25.50m, CatalogNumberInput.Price(valid));
            Assert.Null(CatalogNumberInput.Price(invalid));
            Assert.Null(CatalogNumberInput.Price(""));
            Assert.Null(CatalogNumberInput.Price("1e3"));
            Assert.Null(CatalogNumberInput.Price("not a number"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static ServiceProvider Services(FakeCatalogRepository repository, WorkspaceReads reads) => new ServiceCollection()
        .AddSingleton<ICatalogRepository>(repository).AddSingleton<ICatalogReadRepository>(reads)
        .AddSingleton<IUnitOfWork, FakeUnitOfWork>().AddSingleton<ITransactionManager, TestTransactionManager>()
        .AddSingleton<IClock>(new TestClock()).AddScoped<CatalogCommandHandler>().AddScoped<RecipeCommandHandler>()
        .AddScoped<CatalogQueryHandler>().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    private sealed class WorkspaceReads(FakeCatalogRepository repository) : ICatalogReadRepository
    {
        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<CategorySummary>> SearchCategoriesAsync(CategorySearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult<IReadOnlyList<CategorySummary>>(repository.Categories.Values
                .Select(category => new CategorySummary(category.Id, category.Name, category.DisplayOrder, category.IsActive)).ToArray());
        }

        public Task<IReadOnlyList<MenuItemSummary>> SearchMenuItemsAsync(MenuItemSearchCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MenuItemSummary>>(repository.MenuItems.Values
                .Where(item => criteria.IsActive is null || item.IsActive == criteria.IsActive)
                .Where(item => criteria.Search is null || item.Name.Contains(criteria.Search, StringComparison.OrdinalIgnoreCase))
                .Select(item => new MenuItemSummary(item.Id, item.CategoryId, repository.Categories[item.CategoryId].Name,
                    item.Name, item.Description, item.IsActive, [], [])).Take(criteria.Limit).ToArray());

        public Task<MenuItemDetail?> GetMenuItemDetailAsync(EntityId menuItemId, CancellationToken cancellationToken = default)
        {
            var item = repository.MenuItems.GetValueOrDefault(menuItemId);
            var variants = repository.SizeVariants.Values.Where(variant => variant.MenuItemId == menuItemId)
                .Select(variant => new SizeVariantDetail(variant.Id, variant.SizeName, variant.Price.Amount, variant.IsActive,
                    repository.Recipes.GetValueOrDefault(variant.Id, []).Select(line => new RecipeLineDetail(line.Id,
                        line.IngredientId, repository.Ingredients[line.IngredientId].Name, line.Quantity, line.Unit)).ToArray())).ToArray();
            var addons = repository.Compatibility.Where(link => link.MenuItemId == menuItemId).Select(link => repository.Addons[link.AddonId])
                .Select(addon => new AddonSummary(addon.Id, addon.Name, addon.Price.Amount, addon.IsActive)).ToArray();
            return Task.FromResult(item is null ? null : new MenuItemDetail(item.Id, item.CategoryId,
                repository.Categories[item.CategoryId].Name, item.Name, item.Description, item.IsActive, variants, addons));
        }

        public Task<RecipeDetail?> GetRecipeAsync(EntityId sizeVariantId, CancellationToken cancellationToken = default) => Task.FromResult<RecipeDetail?>(null);

        public Task<IReadOnlyList<AddonSummary>> ListAddonsAsync(CancellationToken cancellationToken = default) => repository.ListAddonsAsync(cancellationToken);

        public Task<IReadOnlyList<RecipeIngredientOption>> ListActiveRecipeIngredientsAsync(CancellationToken cancellationToken = default) => repository.ListActiveRecipeIngredientsAsync(cancellationToken);
    }
}
