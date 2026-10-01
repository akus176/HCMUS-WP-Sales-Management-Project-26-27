namespace CoffeeShop.Application.Common.Time;

using CoffeeShop.Domain.Common;

public interface IClock
{
    UtcTimestamp UtcNow { get; }

    DateTimeOffset DateTimeUtcNow { get; }
}
