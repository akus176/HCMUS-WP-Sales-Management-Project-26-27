namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

// Operates on resolved domain objects; loading and saving belong to catalog use cases.
public static class CatalogLifecycle
{
    public static Result<Category> CreateCategory(EntityId id, string name, UtcTimestamp createdAt, int displayOrder = 0) =>
        Execute(() => new Category(id, name, createdAt, createdAt, displayOrder));

    public static Result<MenuItem> CreateMenuItem(EntityId id, Category category, string name, UtcTimestamp createdAt, string? description = null) =>
        Execute(() => new MenuItem(id, category, name, createdAt, createdAt, description));

    public static Result RenameCategory(Category category, string name, UtcTimestamp changedAt) =>
        Execute(() => category.Rename(name, changedAt));

    public static Result ActivateCategory(Category category, UtcTimestamp changedAt) =>
        Execute(() => category.Activate(changedAt));

    public static Result DeactivateCategory(Category category, UtcTimestamp changedAt) =>
        Execute(() => category.Deactivate(changedAt));

    public static Result UpdateMenuItem(MenuItem item, string name, string? description, Category category, UtcTimestamp changedAt) =>
        Execute(() => item.Update(name, description, category, changedAt));

    public static Result ActivateMenuItem(MenuItem item, Category category, UtcTimestamp changedAt) =>
        Execute(() => item.Activate(category, changedAt));

    public static Result DeactivateMenuItem(MenuItem item, UtcTimestamp changedAt) =>
        Execute(() => item.Deactivate(changedAt));

    private static Result Execute(Action operation) => Execute(() =>
    {
        operation();
        return true;
    });

    private static Result<T> Execute<T>(Func<T> operation)
        where T : notnull
    {
        try
        {
            return Result<T>.Success(operation());
        }
        catch (ArgumentException exception)
        {
            return Result<T>.Failure(Error.Validation(
                "catalog.invalid_input",
                exception.Message,
                [new ErrorDetail(exception.ParamName ?? "catalog", exception.Message)]));
        }
        catch (InvalidOperationException exception)
        {
            return Result<T>.Failure(Error.Conflict("catalog.invalid_state", exception.Message));
        }
    }
}
