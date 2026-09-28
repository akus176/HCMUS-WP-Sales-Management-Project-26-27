using CoffeeShop.Domain.Common;

namespace CoffeeShop.Domain.Tests.Common;

public sealed class MeasuredQuantityTests
{
    [Fact]
    public void Create_WithNegativeValue_ThrowsClearValidationError()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MeasuredQuantity(-0.001m, "kg"));

        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithBlankUnit_ThrowsClearValidationError()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new MeasuredQuantity(1m, "   "));

        Assert.Contains("unit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithMoreThanThreeDecimalPlaces_ThrowsClearValidationError()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new MeasuredQuantity(0.0001m, "kg"));

        Assert.Contains("decimal places", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_AboveDatabasePrecision_ThrowsClearValidationError()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MeasuredQuantity(100_000_000_000m, "kg"));

        Assert.Contains("maximum", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_AtDatabasePrecisionBoundary_IsAllowed()
    {
        var quantity = new MeasuredQuantity(99_999_999_999.999m, "kg");

        Assert.Equal(99_999_999_999.999m, quantity.Value);
    }
}
