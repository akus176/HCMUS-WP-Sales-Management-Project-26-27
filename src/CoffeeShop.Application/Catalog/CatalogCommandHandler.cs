namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public sealed class CatalogCommandHandler
{
    private readonly ICatalogRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITransactionManager transactionManager;
    private readonly IClock clock;

    public CatalogCommandHandler(
        ICatalogRepository repository,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IClock clock)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        this.transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<EntityId>> CreateCategoryAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = CatalogLifecycle.CreateCategory(
            EntityId.New(),
            command.Name,
            clock.UtcNow,
            command.DisplayOrder);
        if (created.IsFailure)
        {
            return Result<EntityId>.Failure(created.Error!);
        }

        repository.AddCategory(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EntityId>.Success(created.Value.Id);
    }

    public async Task<Result> UpdateCategoryAsync(
        UpdateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await repository.FindCategoryAsync(command.CategoryId, cancellationToken);
        if (category is null)
        {
            return CategoryNotFound();
        }

        var updated = CatalogLifecycle.RenameCategory(category, command.Name, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateCategoryAsync(
        DeactivateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await repository.FindCategoryAsync(command.CategoryId, cancellationToken);
        if (category is null)
        {
            return CategoryNotFound();
        }

        var deactivated = CatalogLifecycle.DeactivateCategory(category, clock.UtcNow);
        if (deactivated.IsFailure)
        {
            return deactivated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<EntityId>> CreateMenuItemAsync(
        CreateMenuItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.InitialVariants is null || command.InitialVariants.Count == 0)
        {
            return Result<EntityId>.Failure(Validation(
                "catalog.variants_required",
                "A menu item requires at least one initial size variant.",
                "initialVariants"));
        }

        var category = await repository.FindCategoryAsync(command.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result<EntityId>.Failure(CategoryNotFoundError());
        }

        var createdItem = CatalogLifecycle.CreateMenuItem(
            EntityId.New(),
            category,
            command.Name,
            clock.UtcNow,
            command.Description);
        if (createdItem.IsFailure)
        {
            return Result<EntityId>.Failure(createdItem.Error!);
        }

        var variants = new List<SizeVariant>();
        var recipeInputs = new List<(int Index, IReadOnlyCollection<RecipeLineInput> Lines)>();
        foreach (var input in command.InitialVariants)
        {
            if (input is null)
            {
                return Result<EntityId>.Failure(Validation(
                    "catalog.invalid_input",
                    "Initial size variants cannot contain a null entry.",
                    "initialVariants"));
            }

            var createdVariant = CatalogVariants.CreateSizeVariant(
                EntityId.New(),
                createdItem.Value,
                input.SizeName,
                input.Price,
                variants);
            if (createdVariant.IsFailure)
            {
                return Result<EntityId>.Failure(createdVariant.Error!);
            }

            if (input.Recipe is { Count: > 0 } lines) recipeInputs.Add((variants.Count, lines));
            variants.Add(createdVariant.Value);
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var recipes = new List<RecipeIngredient>();
            if (recipeInputs.Count > 0)
            {
                var ingredientIds = recipeInputs.SelectMany(recipe => recipe.Lines)
                    .Where(line => line is not null).Select(line => line.IngredientId).Distinct().ToArray();
                var ingredients = (await repository.ListIngredientsForRecipeUpdateAsync(ingredientIds, cancellationToken))
                    .ToDictionary(ingredient => ingredient.Id);
                foreach (var (index, lines) in recipeInputs)
                {
                    var recipe = RecipeCommandHandler.BuildRecipe(variants[index], lines, ingredients, $"initialVariants[{index}].recipe");
                    if (recipe.IsFailure) return Result<EntityId>.Failure(recipe.Error!);
                    recipes.AddRange(recipe.Value);
                }
            }

            repository.AddMenuItem(createdItem.Value);
            foreach (var variant in variants)
            {
                repository.AddSizeVariant(variant);
            }
            if (recipes.Count > 0) repository.AddRecipeIngredients(recipes);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<EntityId>.Success(createdItem.Value.Id);
        }, cancellationToken);
    }

    public async Task<Result> UpdateMenuItemAsync(
        UpdateMenuItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = await repository.FindMenuItemAsync(command.MenuItemId, cancellationToken);
        if (item is null)
        {
            return MenuItemNotFound();
        }

        var category = await repository.FindCategoryAsync(command.CategoryId, cancellationToken);
        if (category is null)
        {
            return CategoryNotFound();
        }

        var updated = CatalogLifecycle.UpdateMenuItem(
            item,
            command.Name,
            command.Description,
            category,
            clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateMenuItemAsync(
        DeactivateMenuItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = await repository.FindMenuItemAsync(command.MenuItemId, cancellationToken);
        if (item is null)
        {
            return MenuItemNotFound();
        }

        var deactivated = CatalogLifecycle.DeactivateMenuItem(item, clock.UtcNow);
        if (deactivated.IsFailure)
        {
            return deactivated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<EntityId>> CreateSizeVariantAsync(
        CreateSizeVariantCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = await repository.FindMenuItemAsync(command.MenuItemId, cancellationToken);
        if (item is null)
        {
            return Result<EntityId>.Failure(MenuItemNotFoundError());
        }

        var variants = await repository.ListSizeVariantsAsync(item.Id, cancellationToken);
        var created = CatalogVariants.CreateSizeVariant(
            EntityId.New(),
            item,
            command.SizeName,
            command.Price,
            variants);
        if (created.IsFailure)
        {
            return Result<EntityId>.Failure(created.Error!);
        }

        repository.AddSizeVariant(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EntityId>.Success(created.Value.Id);
    }

    public async Task<Result> UpdateSizeVariantAsync(
        UpdateSizeVariantCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = await repository.FindMenuItemAsync(command.MenuItemId, cancellationToken);
        if (item is null)
        {
            return MenuItemNotFound();
        }

        var variant = await repository.FindSizeVariantAsync(command.SizeVariantId, cancellationToken);
        if (variant is null)
        {
            return SizeVariantNotFound();
        }

        var variants = await repository.ListSizeVariantsAsync(item.Id, cancellationToken);
        var updated = CatalogVariants.UpdateSizeVariant(
            item,
            variant,
            command.SizeName,
            command.Price,
            variants);
        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateSizeVariantAsync(
        DeactivateSizeVariantCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var variant = await repository.FindSizeVariantAsync(command.SizeVariantId, cancellationToken);
        if (variant is null)
        {
            return SizeVariantNotFound();
        }

        var deactivated = CatalogVariants.DeactivateSizeVariant(variant);
        if (deactivated.IsFailure)
        {
            return Result.Failure(deactivated.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<EntityId>> CreateAddonAsync(
        CreateAddonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = CatalogAddons.CreateAddon(EntityId.New(), command.Name, command.Price);
        if (created.IsFailure)
        {
            return Result<EntityId>.Failure(created.Error!);
        }

        repository.AddAddon(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EntityId>.Success(created.Value.Id);
    }

    public async Task<Result> UpdateAddonAsync(
        UpdateAddonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var addon = await repository.FindAddonAsync(command.AddonId, cancellationToken);
        if (addon is null)
        {
            return AddonNotFound();
        }

        var updated = CatalogAddons.UpdateAddon(addon, command.Name, command.Price);
        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAddonAsync(
        DeactivateAddonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var addon = await repository.FindAddonAsync(command.AddonId, cancellationToken);
        if (addon is null)
        {
            return AddonNotFound();
        }

        var deactivated = CatalogAddons.DeactivateAddon(addon);
        if (deactivated.IsFailure)
        {
            return Result.Failure(deactivated.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetMenuItemAddonsAsync(
        SetMenuItemAddonsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AddonIds is null)
        {
            return Result.Failure(Validation(
                "catalog.invalid_input",
                "Compatible add-ons are required.",
                "addonIds"));
        }

        var item = await repository.FindMenuItemAsync(command.MenuItemId, cancellationToken);
        if (item is null)
        {
            return MenuItemNotFound();
        }

        if (command.AddonIds.Distinct().Count() != command.AddonIds.Count)
        {
            return Result.Failure(Validation(
                "catalog.duplicate_addon",
                "Compatible add-ons cannot contain duplicate identifiers.",
                "addonIds"));
        }

        var links = new List<MenuItemAddon>();
        foreach (var addonId in command.AddonIds)
        {
            var addon = await repository.FindAddonAsync(addonId, cancellationToken);
            if (addon is null)
            {
                return Result.Failure(Error.NotFound(
                    "catalog.addon_not_found",
                    $"Add-on '{addonId}' was not found."));
            }

            if (!addon.IsActive)
            {
                return Result.Failure(Error.Conflict(
                    "catalog.addon_inactive",
                    $"Add-on '{addon.Name}' is inactive and cannot be made compatible."));
            }

            links.Add(new MenuItemAddon(item.Id, addon.Id));
        }

        await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            await repository.ReplaceCompatibleAddonsAsync(item.Id, links, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return Result.Success();
    }

    private static Result CategoryNotFound() => Result.Failure(CategoryNotFoundError());

    private static Error CategoryNotFoundError() =>
        Error.NotFound("catalog.category_not_found", "The selected category was not found.");

    private static Result MenuItemNotFound() => Result.Failure(MenuItemNotFoundError());

    private static Error MenuItemNotFoundError() =>
        Error.NotFound("catalog.menu_item_not_found", "The selected menu item was not found.");

    private static Result SizeVariantNotFound() => Result.Failure(
        Error.NotFound("catalog.variant_not_found", "The selected size variant was not found."));

    private static Result AddonNotFound() => Result.Failure(
        Error.NotFound("catalog.addon_not_found", "The selected add-on was not found."));

    private static Error Validation(string code, string description, string target) =>
        Error.Validation(code, description, [new ErrorDetail(target, description)]);
}
