namespace CoffeeShop.Application.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

/// <summary>
/// Records validated point amounts on a resolved customer with its ledger loaded.
/// Order eligibility, settings, discount changes, and transactional persistence belong
/// to the calling use case. This adapter neither reserves points nor saves data.
/// </summary>
public static class LoyaltyLedger
{
    public static Result<LoyaltyTransaction> Earn(
        Customer customer, EntityId transactionId, int points, UtcTimestamp changedAt,
        EntityId? orderId = null, EntityId? createdBy = null) =>
        Execute(customer, () => customer.EarnLoyaltyPoints(transactionId, points, changedAt, orderId, createdBy));

    public static Result<LoyaltyTransaction> Redeem(
        Customer customer, EntityId transactionId, int points, UtcTimestamp changedAt,
        EntityId? orderId = null, EntityId? createdBy = null) =>
        Execute(customer, () => customer.RedeemLoyaltyPoints(transactionId, points, changedAt, orderId, createdBy));

    public static Result<LoyaltyTransaction> Reverse(
        Customer customer, EntityId transactionId, EntityId originalTransactionId,
        UtcTimestamp changedAt, EntityId? createdBy = null) =>
        Execute(customer, () => customer.ReverseLoyaltyTransaction(transactionId, originalTransactionId, changedAt, createdBy));

    private static Result<LoyaltyTransaction> Execute(Customer customer, Func<LoyaltyTransaction> operation)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(customer);
            return Result<LoyaltyTransaction>.Success(operation());
        }
        catch (ArgumentException exception)
        {
            return Result<LoyaltyTransaction>.Failure(Error.Validation(
                "loyalty.invalid_input", exception.Message,
                [new ErrorDetail(exception.ParamName ?? "loyalty", exception.Message)]));
        }
        catch (InvalidOperationException exception)
        {
            return Result<LoyaltyTransaction>.Failure(Error.Conflict("loyalty.invalid_state", exception.Message));
        }
        catch (KeyNotFoundException exception)
        {
            return Result<LoyaltyTransaction>.Failure(Error.NotFound("loyalty.transaction_not_found", exception.Message));
        }
    }
}
