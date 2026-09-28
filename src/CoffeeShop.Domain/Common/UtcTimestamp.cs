namespace CoffeeShop.Domain.Common;

public sealed record UtcTimestamp
{
    public UtcTimestamp(DateTimeOffset value)
    {
        Value = value.ToUniversalTime();
    }

    public DateTimeOffset Value { get; }
}
