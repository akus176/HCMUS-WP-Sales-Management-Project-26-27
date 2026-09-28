using System.Text.Json.Serialization;

namespace CoffeeShop.Domain.Common;

public sealed record Money
{
    public const decimal MaximumAmount = 9_999_999_999.99m;

    public Money(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Money amount cannot be negative.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException("Money amount cannot have more than two decimal places.", nameof(amount));
        }

        if (amount > MaximumAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, $"Money amount cannot exceed the maximum of {MaximumAmount}.");
        }

        Amount = amount;
    }

    [JsonConverter(typeof(MoneyAmountJsonConverter))]
    public decimal Amount { get; }

    public static Money Zero { get; } = new(0m);
}
