namespace CoffeeShop.Application.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;

// Operates on resolved domain objects. Persistence and balance/ledger mutation belong to transaction use cases.
public static class LoyaltyCalculations
{
    public static Result<LoyaltySettings> CreateSettings(
        EntityId id, decimal earnPointsPerCurrencyUnit, decimal redeemValuePerPoint,
        decimal maxRedeemablePercentage, UtcTimestamp updatedAt, EntityId? updatedBy = null) =>
        Execute(() => new LoyaltySettings(id, earnPointsPerCurrencyUnit, redeemValuePerPoint, maxRedeemablePercentage, updatedAt, updatedBy));

    public static Result UpdateSettings(
        LoyaltySettings settings, decimal earnPointsPerCurrencyUnit, decimal redeemValuePerPoint,
        decimal maxRedeemablePercentage, UtcTimestamp updatedAt, EntityId? updatedBy = null) =>
        Execute(() =>
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.Update(earnPointsPerCurrencyUnit, redeemValuePerPoint, maxRedeemablePercentage, updatedAt, updatedBy);
            return settings;
        });

    public static Result<int> CalculateEarnedPoints(Customer customer, Order order, LoyaltySettings settings) =>
        Execute(() => LoyaltyRules.CalculateEarnedPoints(customer, order, settings));

    public static Result<int> CalculateMaximumRedeemablePoints(Customer customer, Order order, LoyaltySettings settings) =>
        Execute(() => LoyaltyRules.CalculateMaximumRedeemablePoints(customer, order, settings));

    public static Result<Money> CalculateRedemptionDiscount(Customer customer, Order order, LoyaltySettings settings, int points) =>
        Execute(() => LoyaltyRules.CalculateRedemptionDiscount(customer, order, settings, points));

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
                "loyalty.invalid_input", exception.Message,
                [new ErrorDetail(exception.ParamName ?? "loyalty", exception.Message)]));
        }
        catch (InvalidOperationException exception)
        {
            return Result<T>.Failure(Error.Conflict("loyalty.invalid_state", exception.Message));
        }
    }
}
