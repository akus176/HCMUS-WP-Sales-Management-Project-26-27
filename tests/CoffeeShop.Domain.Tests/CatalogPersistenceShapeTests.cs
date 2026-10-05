namespace CoffeeShop.Domain.Tests;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class CatalogPersistenceShapeTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.0001")]
    [InlineData("1000000000")]
    public void RecipeIngredient_RejectsNonPositiveExcessPrecisionAndOutOfRangeQuantities(string value)
    {
        var quantity = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var variant = new SizeVariant(EntityId.New(), EntityId.New(), "Large", Money.Zero);
        var ingredient = new Ingredient(EntityId.New(), "Coffee", "g", now, now);
        Assert.ThrowsAny<ArgumentException>(() => new RecipeIngredient(EntityId.New(), variant, ingredient, quantity, "g"));
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("999999999.999")]
    public void RecipeIngredient_AcceptsStorageBoundariesAndNormalizesUnit(string value)
    {
        var quantity = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var variant = new SizeVariant(EntityId.New(), EntityId.New(), "Large", Money.Zero);
        var ingredient = new Ingredient(EntityId.New(), "Coffee", "g", now, now);
        var recipe = new RecipeIngredient(EntityId.New(), variant, ingredient, quantity, " G ");
        Assert.Equal(quantity, recipe.Quantity);
        Assert.Equal("g", recipe.Unit);
    }

    [Fact]
    public void Ingredient_UsesSharedQuantityValidation()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ingredient(EntityId.New(), "Milk", "ml", now, now, availableQuantity: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ingredient(EntityId.New(), "Milk", "ml", now, now, reorderThreshold: -1));
        Assert.Throws<ArgumentException>(() => new Ingredient(EntityId.New(), "Milk", " ", now, now));
        var ingredient = new Ingredient(EntityId.New(), "Milk", " ML ", now, now);
        Assert.Equal("ml", ingredient.BaseUnit);
        Assert.Equal(0m, ingredient.AvailableQuantity);
        Assert.Equal(0m, ingredient.ReorderThreshold);
    }
}
