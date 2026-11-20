namespace CoffeeShop.Contracts;

/// <summary>A versioned category representation without persistence entities.</summary>
public sealed record CategoryResponse(Guid Id, string Name, int DisplayOrder, bool IsActive);

public sealed record CreateCategoryRequest(string Name, int DisplayOrder = 0);

public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record PageQuery(int Page = 1, int PageSize = 20);

public sealed record SizeVariantRequest(string? SizeName, decimal? Price, IReadOnlyList<RecipeLineRequest>? Recipe = null);
public sealed record MenuItemRequest(Guid CategoryId, string? Name, string? Description,
    IReadOnlyList<SizeVariantRequest>? InitialVariants = null);
public sealed record AddonRequest(string? Name, decimal? Price);
public sealed record SetCompatibleAddonsRequest(IReadOnlyList<Guid>? AddonIds);
public sealed record RecipeLineRequest(Guid IngredientId, decimal Quantity, string? Unit);
public sealed record ReplaceRecipeRequest(IReadOnlyList<RecipeLineRequest>? Lines);
public sealed record AddonResponse(Guid Id, string Name, decimal Price, bool IsActive);
public sealed record RecipeLineResponse(Guid Id, Guid IngredientId, string IngredientName, decimal Quantity, string Unit);
public sealed record RecipeResponse(Guid SizeVariantId, Guid MenuItemId, string SizeName,
    IReadOnlyList<RecipeLineResponse> Lines);
public sealed record SizeVariantResponse(Guid Id, string SizeName, decimal Price, bool IsActive,
    IReadOnlyList<RecipeLineResponse>? Recipe = null);
public sealed record MenuItemResponse(Guid Id, Guid CategoryId, string CategoryName, string Name,
    string? Description, bool IsActive, IReadOnlyList<SizeVariantResponse> SizeVariants,
    IReadOnlyList<AddonResponse> CompatibleAddons);
