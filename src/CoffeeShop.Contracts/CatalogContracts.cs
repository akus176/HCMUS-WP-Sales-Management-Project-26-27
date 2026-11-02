namespace CoffeeShop.Contracts;

/// <summary>A versioned category representation without persistence entities.</summary>
public sealed record CategoryResponse(Guid Id, string Name, int DisplayOrder, bool IsActive);

public sealed record CreateCategoryRequest(string Name, int DisplayOrder = 0);

public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record PageQuery(int Page = 1, int PageSize = 20);
