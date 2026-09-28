using CoffeeShop.Domain.Common;

namespace CoffeeShop.Domain.Tests.Common;

public sealed class UtcTimestampTests
{
    [Fact]
    public void Create_WithNonUtcOffset_NormalizesToUtc()
    {
        var localValue = new DateTimeOffset(2026, 9, 28, 14, 30, 0, TimeSpan.FromHours(7));

        var timestamp = new UtcTimestamp(localValue);

        Assert.Equal(TimeSpan.Zero, timestamp.Value.Offset);
        Assert.Equal(localValue.UtcDateTime, timestamp.Value.UtcDateTime);
    }
}
