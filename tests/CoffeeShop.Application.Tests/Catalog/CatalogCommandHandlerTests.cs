namespace CoffeeShop.Application.Tests.Catalog;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public sealed class CatalogCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CategoryCommands_CreateUpdateAndDeactivateCategory()
    {
        var context = CreateContext();

        var created = await context.Handler.CreateCategoryAsync(new CreateCategoryCommand(" Coffee ", 2));
        var updated = await context.Handler.UpdateCategoryAsync(new UpdateCategoryCommand(created.Value, " Espresso "));
        var deactivated = await context.Handler.DeactivateCategoryAsync(new DeactivateCategoryCommand(created.Value));

        Assert.True(created.IsSuccess);
        Assert.True(updated.IsSuccess);
        Assert.True(deactivated.IsSuccess);
        var category = context.Repository.Categories[created.Value];
        Assert.Equal("Espresso", category.Name);
        Assert.Equal(2, category.DisplayOrder);
        Assert.False(category.IsActive);
        Assert.Equal(3, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateCategory_WithInvalidName_ReturnsSharedValidationResult()
    {
        var context = CreateContext();

        var result = await context.Handler.CreateCategoryAsync(new CreateCategoryCommand(" "));

        AssertError(result, ErrorType.Validation, "catalog.invalid_input");
        Assert.Empty(context.Repository.Categories);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateMenuItem_WithInitialVariants_SavesInsideExplicitTransaction()
    {
        var context = CreateContext();
        var category = AddCategory(context.Repository);

        var result = await context.Handler.CreateMenuItemAsync(new CreateMenuItemCommand(
            category.Id,
            " Latte ",
            "Milk coffee",
            [new(" Small ", 30000m), new("Large", 45000m)]));

        Assert.True(result.IsSuccess);
        var item = Assert.Single(context.Repository.MenuItems.Values);
        Assert.Equal(result.Value, item.Id);
        Assert.Equal("Latte", item.Name);
        Assert.Equal(["Large", "Small"], context.Repository.SizeVariants.Values
            .Select(variant => variant.SizeName)
            .OrderBy(name => name)
            .ToArray());
        Assert.Equal(1, context.TransactionManager.ExecutionCount);
        Assert.Equal(1, context.TransactionManager.CommittedCount);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateMenuItem_WithoutCategoryOrVariants_ReturnsSharedErrorsWithoutPersistence()
    {
        var missingCategory = CreateContext();
        var missingCategoryResult = await missingCategory.Handler.CreateMenuItemAsync(new CreateMenuItemCommand(
            EntityId.New(),
            "Latte",
            null,
            [new("Small", 30000m)]));

        var noVariants = CreateContext();
        var category = AddCategory(noVariants.Repository);
        var noVariantsResult = await noVariants.Handler.CreateMenuItemAsync(new CreateMenuItemCommand(
            category.Id,
            "Latte",
            null,
            []));

        AssertError(missingCategoryResult, ErrorType.NotFound, "catalog.category_not_found");
        AssertError(noVariantsResult, ErrorType.Validation, "catalog.variants_required");
        Assert.Empty(missingCategory.Repository.MenuItems);
        Assert.Empty(noVariants.Repository.MenuItems);
        Assert.Equal(0, missingCategory.TransactionManager.ExecutionCount);
        Assert.Equal(0, noVariants.TransactionManager.ExecutionCount);
    }

    [Fact]
    public async Task CreateMenuItem_WithDuplicateVariantNames_ReturnsConflictBeforeTransaction()
    {
        var context = CreateContext();
        var category = AddCategory(context.Repository);

        var result = await context.Handler.CreateMenuItemAsync(new CreateMenuItemCommand(
            category.Id,
            "Latte",
            null,
            [new("Large", 45000m), new("Large", 50000m)]));

        AssertError(result, ErrorType.Conflict, "catalog.invalid_state");
        Assert.Empty(context.Repository.MenuItems);
        Assert.Empty(context.Repository.SizeVariants);
        Assert.Equal(0, context.TransactionManager.ExecutionCount);
    }

    [Fact]
    public async Task MenuItemCommands_UpdateDeactivateAndRejectRepeatedDeactivation()
    {
        var context = CreateContext();
        var category = AddCategory(context.Repository);
        var item = AddMenuItem(context.Repository, category);

        var updated = await context.Handler.UpdateMenuItemAsync(new UpdateMenuItemCommand(
            item.Id,
            category.Id,
            "Iced latte",
            "Cold"));
        var deactivated = await context.Handler.DeactivateMenuItemAsync(new DeactivateMenuItemCommand(item.Id));
        var repeated = await context.Handler.DeactivateMenuItemAsync(new DeactivateMenuItemCommand(item.Id));

        Assert.True(updated.IsSuccess);
        Assert.True(deactivated.IsSuccess);
        Assert.Equal("Iced latte", item.Name);
        Assert.False(item.IsActive);
        AssertError(repeated, ErrorType.Conflict, "catalog.invalid_state");
        Assert.Equal(2, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task MenuItemCommands_WithMissingEntities_ReturnNotFound()
    {
        var context = CreateContext();
        var missingId = EntityId.New();

        var updated = await context.Handler.UpdateMenuItemAsync(new UpdateMenuItemCommand(
            missingId,
            EntityId.New(),
            "Latte",
            null));
        var deactivated = await context.Handler.DeactivateMenuItemAsync(new DeactivateMenuItemCommand(missingId));

        AssertError(updated, ErrorType.NotFound, "catalog.menu_item_not_found");
        AssertError(deactivated, ErrorType.NotFound, "catalog.menu_item_not_found");
    }

    [Fact]
    public async Task SizeVariantCommands_CreateUpdateAndDeactivateVariant()
    {
        var context = CreateContext();
        var category = AddCategory(context.Repository);
        var item = AddMenuItem(context.Repository, category);

        var created = await context.Handler.CreateSizeVariantAsync(new CreateSizeVariantCommand(
            item.Id,
            "Small",
            30000m));
        var updated = await context.Handler.UpdateSizeVariantAsync(new UpdateSizeVariantCommand(
            item.Id,
            created.Value,
            "Medium",
            35000m));
        var deactivated = await context.Handler.DeactivateSizeVariantAsync(new DeactivateSizeVariantCommand(created.Value));

        Assert.True(created.IsSuccess);
        Assert.True(updated.IsSuccess);
        Assert.True(deactivated.IsSuccess);
        var variant = context.Repository.SizeVariants[created.Value];
        Assert.Equal("Medium", variant.SizeName);
        Assert.Equal(35000m, variant.Price.Amount);
        Assert.False(variant.IsActive);
    }

    [Fact]
    public async Task AddonCommands_CreateUpdateAndDeactivateAddon()
    {
        var context = CreateContext();

        var created = await context.Handler.CreateAddonAsync(new CreateAddonCommand(" Pearl ", 8000m));
        var updated = await context.Handler.UpdateAddonAsync(new UpdateAddonCommand(created.Value, "Boba", 9000m));
        var deactivated = await context.Handler.DeactivateAddonAsync(new DeactivateAddonCommand(created.Value));

        Assert.True(created.IsSuccess);
        Assert.True(updated.IsSuccess);
        Assert.True(deactivated.IsSuccess);
        var addon = context.Repository.Addons[created.Value];
        Assert.Equal("Boba", addon.Name);
        Assert.Equal(9000m, addon.Price.Amount);
        Assert.False(addon.IsActive);
    }

    [Fact]
    public async Task SetMenuItemAddons_ReplacesCompatibilityInsideTransaction()
    {
        var context = CreateContext();
        var category = AddCategory(context.Repository);
        var item = AddMenuItem(context.Repository, category);
        var addonOne = AddAddon(context.Repository, "Pearl");
        var addonTwo = AddAddon(context.Repository, "Jelly");

        var result = await context.Handler.SetMenuItemAddonsAsync(new SetMenuItemAddonsCommand(
            item.Id,
            [addonOne.Id, addonTwo.Id]));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, context.Repository.Compatibility.Count);
        Assert.All(context.Repository.Compatibility, link => Assert.Equal(item.Id, link.MenuItemId));
        Assert.Equal(1, context.TransactionManager.ExecutionCount);
        Assert.Equal(1, context.TransactionManager.CommittedCount);
    }

    [Fact]
    public async Task SetMenuItemAddons_RejectsDuplicatesMissingAndInactiveAddons()
    {
        var duplicateContext = CreateContext();
        var duplicateItem = AddMenuItem(duplicateContext.Repository, AddCategory(duplicateContext.Repository));
        var duplicateAddon = AddAddon(duplicateContext.Repository, "Pearl");
        var duplicate = await duplicateContext.Handler.SetMenuItemAddonsAsync(new SetMenuItemAddonsCommand(
            duplicateItem.Id,
            [duplicateAddon.Id, duplicateAddon.Id]));

        var missingContext = CreateContext();
        var missingItem = AddMenuItem(missingContext.Repository, AddCategory(missingContext.Repository));
        var missing = await missingContext.Handler.SetMenuItemAddonsAsync(new SetMenuItemAddonsCommand(
            missingItem.Id,
            [EntityId.New()]));

        var inactiveContext = CreateContext();
        var inactiveItem = AddMenuItem(inactiveContext.Repository, AddCategory(inactiveContext.Repository));
        var inactiveAddon = AddAddon(inactiveContext.Repository, "Jelly", isActive: false);
        var inactive = await inactiveContext.Handler.SetMenuItemAddonsAsync(new SetMenuItemAddonsCommand(
            inactiveItem.Id,
            [inactiveAddon.Id]));

        AssertError(duplicate, ErrorType.Validation, "catalog.duplicate_addon");
        AssertError(missing, ErrorType.NotFound, "catalog.addon_not_found");
        AssertError(inactive, ErrorType.Conflict, "catalog.addon_inactive");
        Assert.Equal(0, duplicateContext.TransactionManager.ExecutionCount);
        Assert.Equal(0, missingContext.TransactionManager.ExecutionCount);
        Assert.Equal(0, inactiveContext.TransactionManager.ExecutionCount);
    }

    private static CommandContext CreateContext()
    {
        var repository = new FakeCatalogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var transactionManager = new TestTransactionManager();
        var handler = new CatalogCommandHandler(
            repository,
            unitOfWork,
            transactionManager,
            new TestClock(Now));
        return new CommandContext(handler, repository, unitOfWork, transactionManager);
    }

    private static Category AddCategory(FakeCatalogRepository repository)
    {
        var timestamp = new UtcTimestamp(Now);
        var category = new Category(EntityId.New(), "Coffee", timestamp, timestamp);
        repository.Categories.Add(category.Id, category);
        return category;
    }

    private static MenuItem AddMenuItem(FakeCatalogRepository repository, Category category)
    {
        var timestamp = new UtcTimestamp(Now);
        var item = new MenuItem(EntityId.New(), category, "Latte", timestamp, timestamp);
        repository.MenuItems.Add(item.Id, item);
        return item;
    }

    private static Addon AddAddon(
        FakeCatalogRepository repository,
        string name,
        bool isActive = true)
    {
        var addon = new Addon(EntityId.New(), name, new Money(8000m), isActive);
        repository.Addons.Add(addon.Id, addon);
        return addon;
    }

    private static void AssertError(Result result, ErrorType type, string code)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(type, result.Error.Type);
        Assert.Equal(code, result.Error.Code);
    }

    private sealed record CommandContext(
        CatalogCommandHandler Handler,
        FakeCatalogRepository Repository,
        FakeUnitOfWork UnitOfWork,
        TestTransactionManager TransactionManager);
}
