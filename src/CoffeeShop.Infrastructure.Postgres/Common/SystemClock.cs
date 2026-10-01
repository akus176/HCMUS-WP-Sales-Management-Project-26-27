namespace CoffeeShop.Infrastructure.Postgres.Common;

using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;

public sealed class SystemClock : IClock
{
    public UtcTimestamp UtcNow => new(DateTimeOffset.UtcNow);

    public DateTimeOffset DateTimeUtcNow => DateTimeOffset.UtcNow;
}
