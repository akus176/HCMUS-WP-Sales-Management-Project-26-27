namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;

public sealed class TestClock : IClock
{
    private DateTimeOffset _utcNow;

    public TestClock(DateTimeOffset? initialTime = null)
    {
        _utcNow = (initialTime ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).ToUniversalTime();
    }

    public UtcTimestamp UtcNow => new(_utcNow);

    public DateTimeOffset DateTimeUtcNow => _utcNow;

    public void Set(DateTimeOffset time)
    {
        _utcNow = time.ToUniversalTime();
    }

    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Cannot advance time backwards.");
        }

        _utcNow = _utcNow.Add(duration);
    }
}
