namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

// Operates on resolved domain objects; loading and saving belong to inventory use cases.
public static class PurchaseReceiptLifecycle
{
    public static Result<PurchaseReceipt> CreateDraftReceipt(
        EntityId id,
        EntityId supplierId,
        string receiptNumber,
        UtcTimestamp createdAt) =>
        Execute(() => new PurchaseReceipt(id, supplierId, receiptNumber, createdAt, createdAt));

    public static Result<PurchaseReceiptLine> AddLine(
        PurchaseReceipt receipt,
        EntityId lineId,
        EntityId ingredientId,
        decimal quantity,
        Money unitCost,
        UtcTimestamp changedAt) =>
        Execute(() => receipt.AddLine(lineId, ingredientId, quantity, unitCost, changedAt));

    public static Result RemoveLine(PurchaseReceipt receipt, EntityId lineId, UtcTimestamp changedAt) =>
        Execute(() => receipt.RemoveLine(lineId, changedAt));

    public static Result CancelReceipt(PurchaseReceipt receipt, UtcTimestamp cancelledAt) =>
        Execute(() => receipt.Cancel(cancelledAt));

    /// <summary>
    /// Posts the receipt and applies each line's quantity to the matching
    /// ingredient's balance. The balance changes are explicit: the caller
    /// supplies the ingredient lookup, and every applied ingredient is
    /// returned so the persistence layer can save exactly the changed
    /// aggregates. Nothing is mutated silently.
    /// </summary>
    /// <param name="receipt">The draft receipt to post.</param>
    /// <param name="resolveIngredient">Resolves an ingredient by id; returning null fails the posting.</param>
    /// <param name="postedAt">The posting timestamp.</param>
    public static Result<IReadOnlyCollection<Ingredient>> PostReceipt(
        PurchaseReceipt receipt,
        Func<EntityId, Ingredient?> resolveIngredient,
        UtcTimestamp postedAt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(resolveIngredient);
        ArgumentNullException.ThrowIfNull(postedAt);

        // Resolve and validate every line before mutating anything, so a
        // failure leaves the receipt and all ingredients untouched.
        var resolved = new List<(PurchaseReceiptLine Line, Ingredient Ingredient)>();
        foreach (var line in receipt.Lines)
        {
            var ingredient = resolveIngredient(line.IngredientId);
            if (ingredient is null)
            {
                return Result<IReadOnlyCollection<Ingredient>>.Failure(Error.NotFound(
                    "inventory.ingredient_not_found",
                    $"Ingredient '{line.IngredientId}' referenced by the receipt was not found."));
            }

            if (!ingredient.CanAcceptUnit(ingredient.BaseUnit))
            {
                return Result<IReadOnlyCollection<Ingredient>>.Failure(Error.Validation(
                    "inventory.incompatible_unit",
                    $"Ingredient '{ingredient.Name}' has an incompatible unit.",
                    [new ErrorDetail("ingredient", ingredient.Id.ToString())]));
            }

            resolved.Add((line, ingredient));
        }

        var postResult = Execute(() => receipt.Post(postedAt));
        if (postResult.IsFailure)
        {
            return Result<IReadOnlyCollection<Ingredient>>.Failure(postResult.Error!);
        }

        var changed = new List<Ingredient>();
        foreach (var (line, ingredient) in resolved)
        {
            var receiveResult = IngredientLifecycle.ReceiveStock(ingredient, line.Quantity, postedAt);
            if (receiveResult.IsFailure)
            {
                return Result<IReadOnlyCollection<Ingredient>>.Failure(Error.Conflict(
                    "inventory.receive_failed",
                    $"Posting failed while receiving '{ingredient.Name}': {receiveResult.Error!.Description}. The operation was rolled back in memory; retry after resolving the conflict."));
            }

            changed.Add(ingredient);
        }

        return Result<IReadOnlyCollection<Ingredient>>.Success(changed);
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
