namespace CoffeeShop.Domain.Tests.Catalog;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class RecipeIngredientTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));

    private static Ingredient CreateIngredient(string baseUnit = "g", bool isActive = true) =>
        new(EntityId.New(), "Ingredient", baseUnit, Now, Now, isActive: isActive);

    private static SizeVariant CreateVariant(EntityId? itemId = null, string name = "Large") =>
        new(EntityId.New(), itemId ?? EntityId.New(), name, Money.Zero);

    private static RecipeIngredient CreateLine(SizeVariant variant, Ingredient ingredient, decimal quantity, string unit) =>
        new(EntityId.New(), variant, ingredient, quantity, unit);

    [Fact]
    public void Create_RejectsMissingIdentitySizeAndIngredient()
    {
        var variant = CreateVariant();
        var ingredient = CreateIngredient();

        Assert.Equal("id", Assert.Throws<ArgumentNullException>(() => new RecipeIngredient(null!, variant, ingredient, 18m, "g")).ParamName);
        Assert.Equal("sizeVariant", Assert.Throws<ArgumentNullException>(() => CreateLine(null!, ingredient, 18m, "g")).ParamName);
        Assert.Equal("ingredient", Assert.Throws<ArgumentNullException>(() => CreateLine(variant, null!, 18m, "g")).ParamName);
    }

    [Fact]
    public void Create_RejectsInactiveIngredient()
    {
        var ingredient = CreateIngredient(isActive: false);

        Assert.Equal("ingredient", Assert.Throws<ArgumentException>(() => CreateLine(CreateVariant(), ingredient, 18m, "g")).ParamName);
    }

    [Theory]
    [InlineData("g", "ml")]
    [InlineData("ml", "g")]
    [InlineData("pcs", "kg")]
    [InlineData("kg", "pcs")]
    [InlineData("g", "oz")]
    [InlineData("unknown", "g")]
    public void Create_RejectsIncompatibleOrUnknownUnits(string baseUnit, string unit)
    {
        Assert.Equal("unit", Assert.Throws<ArgumentException>(() => CreateLine(CreateVariant(), CreateIngredient(baseUnit), 1m, unit)).ParamName);
    }

    [Theory]
    [InlineData("kg", "g", "0.001")]
    [InlineData("l", "ml", "0.001")]
    [InlineData("kg", "g", "1.001")]
    [InlineData("l", "ml", "1.001")]
    [InlineData("g", "kg", "100000000")]
    [InlineData("ml", "l", "100000000")]
    public void Create_RejectsConversionThatCannotFitBaseQuantity(string baseUnit, string unit, string value)
    {
        var quantity = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.ThrowsAny<ArgumentException>(() => CreateLine(CreateVariant(), CreateIngredient(baseUnit), quantity, unit));
    }

    [Theory]
    [InlineData("g", "g", "0.001")]
    [InlineData("g", "g", "999999999.999")]
    [InlineData("g", " KG ", "0.018")]
    [InlineData("kg", "g", "1")]
    [InlineData("ml", "l", "0.25")]
    [InlineData("l", " ML ", "250")]
    [InlineData("pcs", " PCS ", "1")]
    [InlineData("pcs", "pcs", "0.001")]
    [InlineData("g", "kg", "99999999.999")]
    public void Create_PreservesSizeIngredientQuantityAndNormalizedUnit(string baseUnit, string unit, string value)
    {
        var variant = CreateVariant();
        var ingredient = CreateIngredient(baseUnit);
        var quantity = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var line = CreateLine(variant, ingredient, quantity, unit);

        Assert.Equal(variant.Id, line.SizeVariantId);
        Assert.Equal(ingredient.Id, line.IngredientId);
        Assert.Equal(quantity, line.Quantity);
        Assert.Equal(unit.Trim().ToLowerInvariant(), line.Unit);
        Assert.Equal(0m, ingredient.AvailableQuantity);
        Assert.Equal(Now, ingredient.UpdatedAt);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.001")]
    [InlineData("0.0001")]
    [InlineData("1.0001")]
    [InlineData("1000000000")]
    public void Create_RejectsInvalidQuantity(string value)
    {
        var quantity = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.ThrowsAny<ArgumentException>(() => CreateLine(CreateVariant(), CreateIngredient(), quantity, "g"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Create_RejectsMissingUnit(string? unit)
    {
        Assert.Throws<ArgumentException>(() => CreateLine(CreateVariant(), CreateIngredient(), 18m, unit!));
    }

    [Fact]
    public void DifferentSizes_KeepIndependentRecipes()
    {
        var itemId = EntityId.New();
        var small = CreateVariant(itemId, "Small");
        var large = CreateVariant(itemId);
        var ingredient = CreateIngredient();
        var smallLine = CreateLine(small, ingredient, 12m, "g");
        var largeLine = CreateLine(large, ingredient, 18m, "g");

        Assert.NotEqual(smallLine.Id, largeLine.Id);
        Assert.Equal(small.Id, smallLine.SizeVariantId);
        Assert.Equal(large.Id, largeLine.SizeVariantId);
        Assert.Equal(12m, smallLine.Quantity);
        Assert.Equal(18m, largeLine.Quantity);
        small.Update("Extra small", new Money(30000m));
        small.Deactivate();
        ingredient.Deactivate(Now);
        Assert.Equal(small.Id, smallLine.SizeVariantId);
        Assert.Equal(ingredient.Id, smallLine.IngredientId);
        Assert.Equal(12m, smallLine.Quantity);
        Assert.Equal(18m, largeLine.Quantity);
    }

    [Fact]
    public void Create_AllowsEditingInactiveSizeWithReactivatedIngredient()
    {
        var variant = CreateVariant();
        variant.Deactivate();
        var ingredient = CreateIngredient(isActive: false);
        ingredient.Activate(Now);
        var id = EntityId.New();

        var line = new RecipeIngredient(id, variant, ingredient, 18m, "g");

        Assert.Equal(id, line.Id);
        Assert.Equal(variant.Id, line.SizeVariantId);
        Assert.False(variant.IsActive);
    }
}
