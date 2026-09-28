using System.Text.Json;
using CoffeeShop.Domain.Common;

namespace CoffeeShop.Domain.Tests.Common;

public sealed class PrimitiveContractTests
{
    [Fact]
    public void EqualInputs_ProduceEqualValueObjects()
    {
        var idValue = Guid.NewGuid();
        var instant = new DateTimeOffset(2026, 9, 28, 7, 30, 0, TimeSpan.Zero);

        Assert.Equal(new EntityId(idValue), new EntityId(idValue));
        Assert.Equal(new Money(12.50m), new Money(12.50m));
        Assert.Equal(new MeasuredQuantity(0m, " KG "), new MeasuredQuantity(0m, "kg"));
        Assert.Equal(new UtcTimestamp(instant), new UtcTimestamp(instant));
    }

    [Fact]
    public void ValueObjects_RoundTripThroughJsonWithoutLosingEquality()
    {
        AssertJsonRoundTrip(new EntityId(Guid.NewGuid()));
        AssertJsonRoundTrip(new Money(12.50m));
        AssertJsonRoundTrip(new MeasuredQuantity(1.250m, "kg"));
        AssertJsonRoundTrip(new UtcTimestamp(
            new DateTimeOffset(2026, 9, 28, 14, 30, 0, TimeSpan.FromHours(7))));

        var actor = new EntityId(Guid.NewGuid());
        AssertJsonRoundTrip(new AuditMetadata(
            new UtcTimestamp(new DateTimeOffset(2026, 9, 28, 7, 0, 0, TimeSpan.Zero)),
            actor,
            new UtcTimestamp(new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero)),
            actor));
    }

    [Fact]
    public void DecimalValueObjects_SerializeWithTheirStorageScale()
    {
        Assert.Equal("{\"Amount\":12.50}", JsonSerializer.Serialize(new Money(12.5m)));
        Assert.Equal(
            "{\"Value\":1.250,\"Unit\":\"kg\"}",
            JsonSerializer.Serialize(new MeasuredQuantity(1.25m, "kg")));
    }

    private static void AssertJsonRoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        var deserialized = JsonSerializer.Deserialize<T>(json);

        Assert.Equal(value, deserialized);
    }
}
