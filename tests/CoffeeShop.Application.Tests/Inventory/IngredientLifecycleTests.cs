namespace CoffeeShop.Application.Tests.Inventory;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class IngredientLifecycleTests
{
    private static readonly UtcTimestamp Created = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp Changed = new(Created.Value.AddHours(1));

    private static Ingredient CreateIngredient(decimal availableQuantity = 10m, decimal reorderThreshold = 2m, bool isActive = true) =>
        new(EntityId.New(), "Coffee beans", "g", Created, Created, availableQuantity, reorderThreshold, isActive);

    [Fact]
    public void CreateIngredient_WithValidData_ReturnsSuccess()
    {
        var result = IngredientLifecycle.CreateIngredient(EntityId.New(), " Coffee beans ", "g", Created, 10m, 2m);

        Assert.True(result.IsSuccess);
        Assert.Equal("Coffee beans", result.Value.Name);
        Assert.Equal("g", result.Value.BaseUnit);
        Assert.Equal(10m, result.Value.AvailableQuantity);
        Assert.Equal(2m, result.Value.ReorderThreshold);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void CreateIngredient_WithInvalidName_ReturnsValidationError(string? name)
    {
        var result = IngredientLifecycle.CreateIngredient(EntityId.New(), name!, "g", Created);

        AssertValidation(result, "name");
    }

    [Fact]
    public void CreateIngredient_WithNegativeBalance_ReturnsValidationError()
    {
        var result = IngredientLifecycle.CreateIngredient(EntityId.New(), "Coffee beans", "g", Created, availableQuantity: -1m);

        AssertValidation(result, "availableQuantity");
    }

    [Fact]
    public void ReceiveStock_WithValidAmount_ReturnsSuccessAndIncreasesBalance()
    {
        var ingredient = CreateIngredient();

        var result = IngredientLifecycle.ReceiveStock(ingredient, 5m, Changed);

        Assert.True(result.IsSuccess);
        Assert.Equal(15m, ingredient.AvailableQuantity);
        Assert.Equal(Changed, ingredient.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReceiveStock_WithNonPositiveAmount_ReturnsValidationError(decimal amount)
    {
        var ingredient = CreateIngredient();

        var result = IngredientLifecycle.ReceiveStock(ingredient, amount, Changed);

        AssertValidation(result, "amount");
        Assert.Equal(10m, ingredient.AvailableQuantity);
        Assert.Equal(Created, ingredient.UpdatedAt);
    }

    [Fact]
    public void ConsumeStock_WithValidAmount_ReturnsSuccessAndDecreasesBalance()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        var result = IngredientLifecycle.ConsumeStock(ingredient, 4m, Changed);

        Assert.True(result.IsSuccess);
        Assert.Equal(6m, ingredient.AvailableQuantity);
    }

    [Fact]
    public void ConsumeStock_ExceedingBalance_ReturnsConflictWithoutMutation()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        var result = IngredientLifecycle.ConsumeStock(ingredient, 10.001m, Changed);

        AssertConflict(result);
        Assert.Equal(10m, ingredient.AvailableQuantity);
        Assert.Equal(Created, ingredient.UpdatedAt);
    }

    [Fact]
    public void AdjustStock_WithPositiveDelta_ReturnsSuccess()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        var result = IngredientLifecycle.AdjustStock(ingredient, 2.5m, Changed);

        Assert.True(result.IsSuccess);
        Assert.Equal(12.5m, ingredient.AvailableQuantity);
    }

    [Fact]
    public void AdjustStock_WithNegativeDelta_ReturnsSuccess()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        var result = IngredientLifecycle.AdjustStock(ingredient, -3m, Changed);

        Assert.True(result.IsSuccess);
        Assert.Equal(7m, ingredient.AvailableQuantity);
    }

    [Fact]
    public void AdjustStock_WithZeroDelta_ReturnsValidationError()
    {
        var ingredient = CreateIngredient();

        var result = IngredientLifecycle.AdjustStock(ingredient, 0m, Changed);

        AssertValidation(result, "delta");
    }

    [Fact]
    public void AdjustStock_MakingBalanceNegative_ReturnsValidationErrorWithoutMutation()
    {
        var ingredient = CreateIngredient(availableQuantity: 10m);

        var result = IngredientLifecycle.AdjustStock(ingredient, -10.001m, Changed);

        AssertValidation(result, "delta");
        Assert.Equal(10m, ingredient.AvailableQuantity);
    }

    [Fact]
    public void SetReorderThreshold_WithValidValue_ReturnsSuccess()
    {
        var ingredient = CreateIngredient(reorderThreshold: 2m);

        var result = IngredientLifecycle.SetReorderThreshold(ingredient, 5m, Changed);

        Assert.True(result.IsSuccess);
        Assert.Equal(5m, ingredient.ReorderThreshold);
    }

    [Fact]
    public void SetReorderThreshold_WithNegativeValue_ReturnsValidationError()
    {
        var ingredient = CreateIngredient();

        var result = IngredientLifecycle.SetReorderThreshold(ingredient, -1m, Changed);

        AssertValidation(result, "value");
        Assert.Equal(2m, ingredient.ReorderThreshold);
    }

    [Fact]
    public void DeactivateIngredient_ReturnsSuccessAndDeactivates()
    {
        var ingredient = CreateIngredient();

        var result = IngredientLifecycle.DeactivateIngredient(ingredient, Changed);

        Assert.True(result.IsSuccess);
        Assert.False(ingredient.IsActive);
        Assert.Equal(StockStatus.LowStock, ingredient.Status);
    }

    [Fact]
    public void ActivateIngredient_ReturnsSuccessAndActivates()
    {
        var ingredient = CreateIngredient(isActive: false);

        var result = IngredientLifecycle.ActivateIngredient(ingredient, Changed);

        Assert.True(result.IsSuccess);
        Assert.True(ingredient.IsActive);
    }

    [Fact]
    public void ConvertToBaseUnit_WithCompatibleUnit_ReturnsConvertedAmount()
    {
        var ingredient = CreateIngredient();

        var result = IngredientLifecycle.ConvertToBaseUnit(ingredient, 2m, "kg");

        Assert.True(result.IsSuccess);
        Assert.Equal(2000m, result.Value);
    }

    [Fact]
    public void ConvertToBaseUnit_WithUnknownUnit_ReturnsValidationError()
    {
        var ingredient = CreateIngredient();

        var result = IngredientLifecycle.ConvertToBaseUnit(ingredient, 1m, "cups");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("inventory.unknown_unit", result.Error.Code);
        Assert.Equal("unit", Assert.Single(result.Error.Details).Target);
    }

    [Fact]
    public void ConvertToBaseUnit_WithIncompatibleUnit_ReturnsValidationError()
    {
        var ingredient = CreateIngredient();

        var result = IngredientLifecycle.ConvertToBaseUnit(ingredient, 1m, "ml");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("inventory.incompatible_unit", result.Error.Code);
        Assert.Equal("unit", Assert.Single(result.Error.Details).Target);
    }

    private static void AssertValidation(Result result, string target)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("inventory.invalid_input", result.Error.Code);
        Assert.Equal(target, Assert.Single(result.Error.Details).Target);
    }

    private static void AssertConflict(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("inventory.invalid_state", result.Error.Code);
    }
}
