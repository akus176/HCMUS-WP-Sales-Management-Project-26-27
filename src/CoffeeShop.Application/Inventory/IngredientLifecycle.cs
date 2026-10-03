namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

// Operates on resolved domain objects; loading and saving belong to inventory use cases.
public static class IngredientLifecycle
{
    public static Result<Ingredient> CreateIngredient(
        EntityId id,
        string name,
        string baseUnit,
        UtcTimestamp createdAt,
        decimal availableQuantity = 0,
        decimal reorderThreshold = 0,
        bool isActive = true) =>
        Execute(() => new Ingredient(id, name, baseUnit, createdAt, createdAt, availableQuantity, reorderThreshold, isActive));

    public static Result ReceiveStock(Ingredient ingredient, decimal amount, UtcTimestamp occurredAt) =>
        Execute(() => ingredient.Receive(amount, occurredAt));

    public static Result ConsumeStock(Ingredient ingredient, decimal amount, UtcTimestamp occurredAt) =>
        Execute(() => ingredient.Consume(amount, occurredAt));

    public static Result AdjustStock(Ingredient ingredient, decimal delta, UtcTimestamp occurredAt) =>
        Execute(() => ingredient.Adjust(delta, occurredAt));

    public static Result SetReorderThreshold(Ingredient ingredient, decimal value, UtcTimestamp occurredAt) =>
        Execute(() => ingredient.SetReorderThreshold(value, occurredAt));

    public static Result DeactivateIngredient(Ingredient ingredient, UtcTimestamp occurredAt) =>
        Execute(() => ingredient.Deactivate(occurredAt));

    public static Result ActivateIngredient(Ingredient ingredient, UtcTimestamp occurredAt) =>
        Execute(() => ingredient.Activate(occurredAt));

    /// <summary>
    /// Converts an amount expressed in the given unit into the ingredient's
    /// base unit. Returns a validation error when the unit is unknown or
    /// incompatible with the ingredient's base unit.
    /// </summary>
    public static Result<decimal> ConvertToBaseUnit(Ingredient ingredient, decimal amount, string unit)
    {
        ArgumentNullException.ThrowIfNull(ingredient);

        if (!IngredientUnit.IsKnown(unit))
        {
            return Result<decimal>.Failure(Error.Validation(
                "inventory.unknown_unit",
                $"Unit '{unit}' is not a supported measurement unit.",
                [new ErrorDetail("unit", unit)]));
        }

        if (!ingredient.CanAcceptUnit(unit))
        {
            return Result<decimal>.Failure(Error.Validation(
                "inventory.incompatible_unit",
                $"Unit '{unit}' is incompatible with the ingredient's base unit '{ingredient.BaseUnit}'.",
                [new ErrorDetail("unit", unit)]));
        }

        var converted = ingredient.ConvertToBaseUnit(amount, unit);
        return converted is null
            ? Result<decimal>.Failure(Error.Validation(
                "inventory.incompatible_unit",
                $"Unit '{unit}' is incompatible with the ingredient's base unit '{ingredient.BaseUnit}'.",
                [new ErrorDetail("unit", unit)]))
            : Result<decimal>.Success(converted.Value);
    }

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
