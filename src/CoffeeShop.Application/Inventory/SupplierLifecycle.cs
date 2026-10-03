namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

// Operates on resolved domain objects; loading and saving belong to inventory use cases.
public static class SupplierLifecycle
{
    public static Result<Supplier> CreateSupplier(
        EntityId id,
        string name,
        string phone,
        UtcTimestamp createdAt,
        string? email = null,
        string? address = null,
        bool isActive = true) =>
        Execute(() => new Supplier(id, name, phone, createdAt, createdAt, email, address, isActive));

    public static Result UpdateSupplier(Supplier supplier, string name, string phone, string? email, string? address, UtcTimestamp changedAt) =>
        Execute(() => supplier.Update(name, phone, email, address, changedAt));

    public static Result DeactivateSupplier(Supplier supplier, UtcTimestamp changedAt) =>
        Execute(() => supplier.Deactivate(changedAt));

    public static Result ActivateSupplier(Supplier supplier, UtcTimestamp changedAt) =>
        Execute(() => supplier.Activate(changedAt));

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
                "inventory.invalid_input",
                exception.Message,
                [new ErrorDetail(exception.ParamName ?? "inventory", exception.Message)]));
        }
        catch (InvalidOperationException exception)
        {
            return Result<T>.Failure(Error.Conflict("inventory.invalid_state", exception.Message));
        }
    }
}
