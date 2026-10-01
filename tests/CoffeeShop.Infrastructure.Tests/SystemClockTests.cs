namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Common.Time;
using CoffeeShop.Infrastructure.Postgres.Common;

public class SystemClockTests
{
    [Fact]
    public void SystemClock_ImplementsIClock()
    {
        var clock = new SystemClock();

        Assert.IsAssignableFrom<IClock>(clock);
    }

    [Fact]
    public void UtcNow_ReturnsCurrentUtcTimestamp()
    {
        var clock = new SystemClock();
        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.True(now.Value >= before);
        Assert.True(now.Value <= after);
        Assert.Equal(TimeSpan.Zero, now.Value.Offset);
    }

    [Fact]
    public void DateTimeUtcNow_ReturnsCurrentDateTimeOffset()
    {
        var clock = new SystemClock();
        var before = DateTimeOffset.UtcNow;
        var now = clock.DateTimeUtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.True(now >= before);
        Assert.True(now <= after);
        Assert.Equal(TimeSpan.Zero, now.Offset);
    }
}
