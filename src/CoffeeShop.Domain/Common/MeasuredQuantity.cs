using System.Text.Json.Serialization;

namespace CoffeeShop.Domain.Common;

public sealed record MeasuredQuantity
{
    public const decimal MaximumValue = 99_999_999_999.999m;

    public MeasuredQuantity(decimal value, string unit)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Measured quantity cannot be negative.");
        }

        if (decimal.Round(value, 3) != value)
        {
            throw new ArgumentException("Measured quantity cannot have more than three decimal places.", nameof(value));
        }

        if (value > MaximumValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Measured quantity cannot exceed the maximum of {MaximumValue}.");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Measurement unit is required.", nameof(unit));
        }

        Value = value;
        Unit = unit.Trim().ToLowerInvariant();
    }

    [JsonConverter(typeof(MeasuredQuantityValueJsonConverter))]
    public decimal Value { get; }

    public string Unit { get; }
}
