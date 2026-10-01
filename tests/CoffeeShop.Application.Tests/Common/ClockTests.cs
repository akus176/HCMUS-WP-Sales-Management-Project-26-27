namespace CoffeeShop.Application.Tests.Common;

using CoffeeShop.Application.Tests.Testing;

public class ClockTests
{
    [Fact]
    public void TestClock_DefaultConstructor_SetsDefaultUtcInstant()
    {
        var clock = new TestClock();

        var expected = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, clock.DateTimeUtcNow);
        Assert.Equal(expected, clock.UtcNow.Value);
    }

    [Fact]
    public void TestClock_CustomConstructor_ConvertsToUtc()
    {
        var localTime = new DateTimeOffset(2026, 5, 10, 15, 30, 0, TimeSpan.FromHours(7));
        var clock = new TestClock(localTime);

        Assert.Equal(localTime.ToUniversalTime(), clock.DateTimeUtcNow);
        Assert.Equal(TimeSpan.Zero, clock.DateTimeUtcNow.Offset);
        Assert.Equal(localTime.ToUniversalTime(), clock.UtcNow.Value);
    }

    [Fact]
    public void TestClock_Set_UpdatesCurrentTimeInUtc()
    {
        var clock = new TestClock();
        var newTime = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(2));

        clock.Set(newTime);

        Assert.Equal(newTime.ToUniversalTime(), clock.DateTimeUtcNow);
        Assert.Equal(newTime.ToUniversalTime(), clock.UtcNow.Value);
    }

    [Fact]
    public void TestClock_Advance_IncreasesTimeByGivenDuration()
    {
        var initial = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(initial);

        clock.Advance(TimeSpan.FromMinutes(45));

        var expected = initial.AddMinutes(45);
        Assert.Equal(expected, clock.DateTimeUtcNow);
        Assert.Equal(expected, clock.UtcNow.Value);
    }

    [Fact]
    public void TestClock_Advance_WithNegativeDuration_ThrowsArgumentOutOfRangeException()
    {
        var clock = new TestClock();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromMinutes(-5)));
    }
}
