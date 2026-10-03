namespace CoffeeShop.Domain.Tests.Inventory;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class IngredientTests
{
    private static readonly UtcTimestamp CreatedAt = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static Ingredient CreateIngredient(
        decimal availableQuantity = 10m,
        decimal reorderThreshold = 2m,
        bool isActive = true) =>
        new(
            EntityId.New(),
            "Coffee beans",
            "g",
            CreatedAt,
            CreatedAt,
            availableQuantity,
            reorderThreshold,
            isActive);

    [Fact]
    public void Constructor_AcceptsValidValues()
    {
        var ingredient = CreateIngredient();

        Assert.Equal("coffee beans", ingredient.Name.ToLowerInvariant());
        Assert.Equal("g", ingredient.BaseUnit);
        Assert.Equal(10m, ingredient.AvailableQuantity);
        Assert.Equal(2m, ingredient.ReorderThreshold);
        Assert.True(ingredient.IsActive);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.001)]
    public void Constructor_RejectsNegativeBalance(decimal balance)
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateIngredient(availableQuantity: balance));
    }

    [Fact]
    public void Constructor_RejectsExcessPrecision()
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateIngredient(availableQuantity: 1.0001m));
    }

    [Fact]
    public void Constructor_RejectsValueAboveMaximum()
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateIngredient(availableQuantity: MeasuredQuantity.MaximumValue + 1));
    }

    [Fact]
    public void Receive_IncreasesBalance()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);
        var at = new UtcTimestamp(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

        ingredient.Receive(5m, at);

        Assert.Equal(15m, ingredient.AvailableQuantity);
        Assert.Equal(at, ingredient.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Receive_RejectsNonPositiveAmount(decimal amount)
    {
        var ingredient = CreateIngredient();

        Assert.ThrowsAny<ArgumentException>(() => ingredient.Receive(amount, CreatedAt));
    }

    [Fact]
    public void Receive_RejectsExcessPrecision()
    {
        var ingredient = CreateIngredient();

        Assert.ThrowsAny<ArgumentException>(() => ingredient.Receive(1.0001m, CreatedAt));
    }

    [Fact]
    public void Receive_RejectsAmountAboveMaximum()
    {
        var ingredient = CreateIngredient();

        Assert.ThrowsAny<ArgumentException>(() => ingredient.Receive(MeasuredQuantity.MaximumValue + 1, CreatedAt));
    }

    [Fact]
    public void Consume_DecreasesBalance()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);
        var at = new UtcTimestamp(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

        ingredient.Consume(4m, at);

        Assert.Equal(6m, ingredient.AvailableQuantity);
        Assert.Equal(at, ingredient.UpdatedAt);
    }

    [Fact]
    public void Consume_AllowsConsumingEntireBalance()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        ingredient.Consume(10m, CreatedAt);

        Assert.Equal(0m, ingredient.AvailableQuantity);
    }

    [Fact]
    public void Consume_RejectsAmountExceedingBalance()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        Assert.Throws<InvalidOperationException>(() => ingredient.Consume(10.001m, CreatedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Consume_RejectsNonPositiveAmount(decimal amount)
    {
        var ingredient = CreateIngredient();

        Assert.ThrowsAny<ArgumentException>(() => ingredient.Consume(amount, CreatedAt));
    }

    [Fact]
    public void Adjust_IncreasesBalanceWithPositiveDelta()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        ingredient.Adjust(2.5m, CreatedAt);

        Assert.Equal(12.5m, ingredient.AvailableQuantity);
    }

    [Fact]
    public void Adjust_DecreasesBalanceWithNegativeDelta()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        ingredient.Adjust(-3m, CreatedAt);

        Assert.Equal(7m, ingredient.AvailableQuantity);
    }

    [Fact]
    public void Adjust_RejectsZeroDelta()
    {
        var ingredient = CreateIngredient();

        Assert.Throws<ArgumentException>(() => ingredient.Adjust(0m, CreatedAt));
    }

    [Fact]
    public void Adjust_RejectsDeltaMakingBalanceNegative()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        Assert.ThrowsAny<ArgumentException>(() => ingredient.Adjust(-10.001m, CreatedAt));
    }

    [Fact]
    public void Adjust_RejectsExcessPrecision()
    {
        var ingredient = CreateIngredient();

        Assert.ThrowsAny<ArgumentException>(() => ingredient.Adjust(0.0001m, CreatedAt));
    }

    [Fact]
    public void SetReorderThreshold_UpdatesThreshold()
    {
        var ingredient = CreateIngredient(reorderThreshold: 2m);

        ingredient.SetReorderThreshold(5m, CreatedAt);

        Assert.Equal(5m, ingredient.ReorderThreshold);
    }

    [Fact]
    public void SetReorderThreshold_RejectsNegativeValue()
    {
        var ingredient = CreateIngredient();

        Assert.ThrowsAny<ArgumentException>(() => ingredient.SetReorderThreshold(-1m, CreatedAt));
    }

    [Fact]
    public void SetReorderThreshold_RejectsExcessPrecision()
    {
        var ingredient = CreateIngredient();

        Assert.ThrowsAny<ArgumentException>(() => ingredient.SetReorderThreshold(1.0001m, CreatedAt));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var ingredient = CreateIngredient();

        ingredient.Deactivate(CreatedAt);

        Assert.False(ingredient.IsActive);
        Assert.Equal(CreatedAt, ingredient.UpdatedAt);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var ingredient = CreateIngredient(isActive: false);

        ingredient.Activate(CreatedAt);

        Assert.True(ingredient.IsActive);
        Assert.Equal(CreatedAt, ingredient.UpdatedAt);
    }

    [Theory]
    [InlineData("g", "kg")]
    [InlineData("kg", "g")]
    [InlineData("ml", "l")]
    [InlineData("l", "ml")]
    [InlineData("g", "g")]
    [InlineData("pcs", "pcs")]
    public void IngredientUnit_AreCompatible_AcceptsSameFamilyUnits(string source, string target)
    {
        Assert.True(IngredientUnit.AreCompatible(source, target));
    }

    [Theory]
    [InlineData("g", "ml")]
    [InlineData("g", "l")]
    [InlineData("kg", "pcs")]
    [InlineData("ml", "pcs")]
    [InlineData("g", "pcs")]
    public void IngredientUnit_AreCompatible_RejectsCrossFamilyUnits(string source, string target)
    {
        Assert.False(IngredientUnit.AreCompatible(source, target));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bushels")]
    [InlineData("cups")]
    public void IngredientUnit_AreCompatible_RejectsUnknownUnits(string unit)
    {
        Assert.False(IngredientUnit.AreCompatible(unit, "g"));
        Assert.False(IngredientUnit.AreCompatible("g", unit));
    }

    [Theory]
    [InlineData(1, "kg", "g", 1000)]
    [InlineData(2.5, "kg", "g", 2500)]
    [InlineData(1500, "g", "kg", 1.5)]
    [InlineData(1, "l", "ml", 1000)]
    [InlineData(500, "ml", "l", 0.5)]
    [InlineData(3, "pcs", "pcs", 3)]
    public void IngredientUnit_Convert_ConvertsWithinFamily(decimal value, string from, string to, decimal expected)
    {
        var result = IngredientUnit.Convert(value, from, to);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void IngredientUnit_Convert_ReturnsNullForCrossFamilyConversion()
    {
        Assert.Null(IngredientUnit.Convert(1, "g", "ml"));
    }

    [Fact]
    public void IngredientUnit_Convert_ReturnsNullForUnknownUnit()
    {
        Assert.Null(IngredientUnit.Convert(1, "cups", "g"));
    }

    [Fact]
    public void IngredientUnit_FamilyOf_ReturnsCanonicalUnit()
    {
        Assert.Equal("g", IngredientUnit.FamilyOf("kg"));
        Assert.Equal("g", IngredientUnit.FamilyOf("G"));
        Assert.Equal("ml", IngredientUnit.FamilyOf("l"));
        Assert.Equal("pcs", IngredientUnit.FamilyOf("pcs"));
        Assert.Null(IngredientUnit.FamilyOf("cups"));
    }

    [Fact]
    public void IngredientUnit_IsKnown_AcceptsCaseInsensitiveUnits()
    {
        Assert.True(IngredientUnit.IsKnown("KG"));
        Assert.True(IngredientUnit.IsKnown(" ml "));
        Assert.False(IngredientUnit.IsKnown("cups"));
        Assert.False(IngredientUnit.IsKnown(""));
    }

    [Fact]
    public void CanAcceptUnit_AcceptsSameFamilyUnit()
    {
        var ingredient = CreateIngredient();

        Assert.True(ingredient.CanAcceptUnit("kg"));
        Assert.True(ingredient.CanAcceptUnit("g"));
    }

    [Fact]
    public void CanAcceptUnit_RejectsCrossFamilyUnit()
    {
        var ingredient = CreateIngredient();

        Assert.False(ingredient.CanAcceptUnit("ml"));
        Assert.False(ingredient.CanAcceptUnit("pcs"));
    }

    [Fact]
    public void ConvertToBaseUnit_ConvertsKilogramsToGrams()
    {
        var ingredient = CreateIngredient();

        var result = ingredient.ConvertToBaseUnit(2m, "kg");

        Assert.NotNull(result);
        Assert.Equal(2000m, result.Value);
    }

    [Fact]
    public void ConvertToBaseUnit_ReturnsNullForIncompatibleUnit()
    {
        var ingredient = CreateIngredient();

        Assert.Null(ingredient.ConvertToBaseUnit(5m, "ml"));
    }

    [Theory]
    [InlineData(3, 2, true, StockStatus.Ok)]        // above threshold, active
    [InlineData(2, 2, true, StockStatus.LowStock)]  // exactly at threshold, active
    [InlineData(1, 2, true, StockStatus.LowStock)]  // below threshold, active
    [InlineData(0, 2, true, StockStatus.LowStock)]  // empty balance, active
    [InlineData(3, 2, false, StockStatus.LowStock)] // above threshold, deactivated
    [InlineData(0, 0, false, StockStatus.LowStock)] // deactivated with zero threshold
    public void Status_FollowsDocumentedBoundaryRule(
        decimal availableQuantity,
        decimal reorderThreshold,
        bool isActive,
        StockStatus expected)
    {
        var ingredient = CreateIngredient(
            availableQuantity: availableQuantity,
            reorderThreshold: reorderThreshold,
            isActive: isActive);

        Assert.Equal(expected, ingredient.Status);
        Assert.Equal(expected == StockStatus.LowStock, ingredient.IsLowStock);
    }

    [Fact]
    public void Status_AtThresholdCountsAsLowStock()
    {
        var ingredient = CreateIngredient(availableQuantity: 5m, reorderThreshold: 5m);

        Assert.Equal(StockStatus.LowStock, ingredient.Status);
    }

    [Fact]
    public void Status_AboveThresholdIsOk()
    {
        var ingredient = CreateIngredient(availableQuantity: 5.001m, reorderThreshold: 5m);

        Assert.Equal(StockStatus.Ok, ingredient.Status);
    }

    [Fact]
    public void Status_DeactivatedIngredientNeverReportsOk()
    {
        var ingredient = CreateIngredient(availableQuantity: 100m, reorderThreshold: 2m, isActive: false);

        Assert.Equal(StockStatus.LowStock, ingredient.Status);
    }

    [Fact]
    public void Status_ReactivingIngredientRestoresEvaluation()
    {
        var ingredient = CreateIngredient(availableQuantity: 100m, reorderThreshold: 2m, isActive: false);
        Assert.Equal(StockStatus.LowStock, ingredient.Status);

        ingredient.Activate(CreatedAt);

        Assert.Equal(StockStatus.Ok, ingredient.Status);
    }

    [Fact]
    public void Status_ReflectsBalanceChangesImmediately()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m, reorderThreshold: 2m);
        Assert.Equal(StockStatus.Ok, ingredient.Status);

        ingredient.Consume(8.5m, CreatedAt);

        Assert.Equal(StockStatus.LowStock, ingredient.Status);
    }

    [Fact]
    public void Status_ReflectsThresholdChangesImmediately()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m, reorderThreshold: 2m);
        Assert.Equal(StockStatus.Ok, ingredient.Status);

        ingredient.SetReorderThreshold(10m, CreatedAt);

        Assert.Equal(StockStatus.LowStock, ingredient.Status);
    }
}
