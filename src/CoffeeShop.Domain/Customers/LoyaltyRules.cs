namespace CoffeeShop.Domain.Customers;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

/// <summary>
/// Calculates loyalty eligibility and values without mutating balances, orders, or history.
/// Transaction workflows must revalidate these rules when applying a quote.
/// </summary>
public static class LoyaltyRules
{
    public static int CalculateEarnedPoints(Customer customer, Order order, LoyaltySettings settings)
    {
        ValidateCustomer(customer, order, settings);
        if (order.Status != OrderStatus.Completed || order.PaidAmount.Amount < order.Total.Amount)
        {
            throw new InvalidOperationException("Loyalty earning requires a fully paid, completed order.");
        }

        // Total is the final payable amount after ordinary and loyalty discounts; overpayment earns nothing extra.
        var points = decimal.Floor(order.Total.Amount * settings.EarnPointsPerCurrencyUnit);
        if (points > int.MaxValue - customer.LoyaltyPointsBalance)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Earned points would exceed the supported customer balance.");
        }

        return (int)points;
    }

    public static int CalculateMaximumRedeemablePoints(Customer customer, Order order, LoyaltySettings settings)
    {
        ValidateRedemption(customer, order, settings);
        if (settings.RedeemValuePerPoint == 0 || settings.MaxRedeemablePercentage == 0)
        {
            return 0;
        }

        // Round the money cap down, then require the exact conversion to fit within it.
        var cap = RoundDown(order.Total.Amount * settings.MaxRedeemablePercentage / 100m);
        var points = (int)decimal.Min(customer.LoyaltyPointsBalance, decimal.Floor(cap / settings.RedeemValuePerPoint));
        return RoundDown(points * settings.RedeemValuePerPoint) == 0 ? 0 : points;
    }

    public static Money CalculateRedemptionDiscount(Customer customer, Order order, LoyaltySettings settings, int points)
    {
        var maximum = CalculateMaximumRedeemablePoints(customer, order, settings);
        if (points <= 0 || points > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Redeemed points must be positive and within the available balance and order limit.");
        }

        var discount = RoundDown(points * settings.RedeemValuePerPoint);
        if (discount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Redeemed points must convert to at least 0.01 in order value.");
        }

        return new Money(discount);
    }

    private static decimal RoundDown(decimal amount) => decimal.Floor(amount * 100m) / 100m;

    private static void ValidateCustomer(Customer customer, Order order, LoyaltySettings settings)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(settings);
        if (order.CustomerId != customer.Id)
        {
            throw new ArgumentException("The customer must be attached to the order.", nameof(customer));
        }

        if (!customer.IsActive)
        {
            throw new InvalidOperationException("Inactive customers cannot earn or redeem loyalty points.");
        }
    }

    private static void ValidateRedemption(Customer customer, Order order, LoyaltySettings settings)
    {
        ValidateCustomer(customer, order, settings);
        if (order.Status is not (OrderStatus.Draft or OrderStatus.Confirmed))
        {
            throw new InvalidOperationException("Loyalty redemption requires a draft or confirmed order.");
        }

        if (order.LoyaltyPointsRedeemed != 0 || order.LoyaltyDiscountAmount.Amount != 0)
        {
            throw new InvalidOperationException("The order already has a loyalty redemption.");
        }
    }
}
