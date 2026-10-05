namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Domain.Common;

public sealed record CategorySearchCriteria(
    string? Search = null,
    bool? IsActive = null,
    int Limit = 50);

public sealed record CategorySummary(
    EntityId Id,
    string Name,
    int DisplayOrder,
    bool IsActive);

public sealed record MenuItemSearchCriteria(
    string? Search = null,
    EntityId? CategoryId = null,
    bool? IsActive = null,
    int Limit = 50);

public sealed record SizeVariantSummary(
    EntityId Id,
    string SizeName,
    decimal Price,
    bool IsActive);

public sealed record AddonSummary(
    EntityId Id,
    string Name,
    decimal Price,
    bool IsActive);

public sealed record MenuItemSummary(
    EntityId Id,
    EntityId CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<SizeVariantSummary> SizeVariants,
    IReadOnlyList<AddonSummary> CompatibleAddons);

public sealed record RecipeLineDetail(
    EntityId Id,
    EntityId IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit);

public sealed record RecipeDetail(
    EntityId SizeVariantId,
    EntityId MenuItemId,
    string SizeName,
    IReadOnlyList<RecipeLineDetail> Lines);

public sealed record SizeVariantDetail(
    EntityId Id,
    string SizeName,
    decimal Price,
    bool IsActive,
    IReadOnlyList<RecipeLineDetail> Recipe);

public sealed record MenuItemDetail(
    EntityId Id,
    EntityId CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<SizeVariantDetail> SizeVariants,
    IReadOnlyList<AddonSummary> CompatibleAddons);
