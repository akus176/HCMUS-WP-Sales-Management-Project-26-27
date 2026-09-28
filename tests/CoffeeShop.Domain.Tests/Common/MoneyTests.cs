using CoffeeShop.Domain.Common;

namespace CoffeeShop.Domain.Tests.Common;

public sealed class MoneyTests
{
    [Fact]
    public void Create_WithNegativeAmount_ThrowsClearValidationError()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-0.01m));

        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithMoreThanTwoDecimalPlaces_ThrowsClearValidationError()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Money(1.001m));

        Assert.Contains("decimal places", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_AboveDatabasePrecision_ThrowsClearValidationError()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Money(10_000_000_000m));

        Assert.Contains("maximum", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_AtDatabasePrecisionBoundary_IsAllowed()
    {
        var money = new Money(9_999_999_999.99m);

        Assert.Equal(9_999_999_999.99m, money.Amount);
    }
}
