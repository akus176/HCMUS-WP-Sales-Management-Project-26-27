namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Domain.Common;

public sealed record CreateCategoryCommand(string Name, int DisplayOrder = 0);

public sealed record UpdateCategoryCommand(EntityId CategoryId, string Name);

public sealed record DeactivateCategoryCommand(EntityId CategoryId);

public sealed record CreateSizeVariantInput(string SizeName, decimal? Price);

public sealed record CreateMenuItemCommand(
    EntityId CategoryId,
    string Name,
    string? Description,
    IReadOnlyCollection<CreateSizeVariantInput> InitialVariants);

public sealed record UpdateMenuItemCommand(
    EntityId MenuItemId,
    EntityId CategoryId,
    string Name,
    string? Description);

public sealed record DeactivateMenuItemCommand(EntityId MenuItemId);

public sealed record CreateSizeVariantCommand(EntityId MenuItemId, string SizeName, decimal? Price);

public sealed record UpdateSizeVariantCommand(
    EntityId MenuItemId,
    EntityId SizeVariantId,
    string SizeName,
    decimal? Price);

public sealed record DeactivateSizeVariantCommand(EntityId SizeVariantId);

public sealed record CreateAddonCommand(string Name, decimal? Price);

public sealed record UpdateAddonCommand(EntityId AddonId, string Name, decimal? Price);

public sealed record DeactivateAddonCommand(EntityId AddonId);

public sealed record SetMenuItemAddonsCommand(
    EntityId MenuItemId,
    IReadOnlyCollection<EntityId> AddonIds);

public sealed record RecipeLineInput(EntityId IngredientId, decimal Quantity, string Unit);

public sealed record ReplaceRecipeCommand(
    EntityId SizeVariantId,
    IReadOnlyCollection<RecipeLineInput> Lines);
