namespace CoffeeShop.Application.Tests.Inventory;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class SupplierLifecycleTests
{
    private static readonly UtcTimestamp Created = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp Changed = new(Created.Value.AddHours(1));

    [Fact]
    public void CreateSupplier_WithValidData_ReturnsSuccess()
    {
        var result = SupplierLifecycle.CreateSupplier(EntityId.New(), " Bean Roasters ", "0901234567", Created, "sales@beans.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("Bean Roasters", result.Value.Name);
        Assert.Equal("sales@beans.com", result.Value.Email);
        Assert.True(result.Value.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateSupplier_WithInvalidName_ReturnsValidationError(string? name)
    {
        var result = SupplierLifecycle.CreateSupplier(EntityId.New(), name!, "0901234567", Created);

        AssertValidation(result, "name");
    }

    [Fact]
    public void UpdateSupplier_WithValidData_ReturnsSuccessAndUpdates()
    {
        var supplier = CreateSupplier();

        var result = SupplierLifecycle.UpdateSupplier(supplier, "New Roasters", "0987654321", null, null, Changed);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Roasters", supplier.Name);
        Assert.Equal(Changed, supplier.UpdatedAt);
    }

    [Fact]
    public void UpdateSupplier_WithInvalidPhone_ReturnsValidationErrorWithoutMutation()
    {
        var supplier = CreateSupplier();

        var result = SupplierLifecycle.UpdateSupplier(supplier, "New Roasters", "", null, null, Changed);

        AssertValidation(result, "phone");
        Assert.Equal("Bean Roasters", supplier.Name);
        Assert.Equal("0901234567", supplier.Phone);
        Assert.Equal(Created, supplier.UpdatedAt);
    }

    [Fact]
    public void DeactivateSupplier_ReturnsSuccessAndDeactivates()
    {
        var supplier = CreateSupplier();

        var result = SupplierLifecycle.DeactivateSupplier(supplier, Changed);

        Assert.True(result.IsSuccess);
        Assert.False(supplier.IsActive);
        Assert.Equal("Bean Roasters", supplier.Name);
    }

    [Fact]
    public void ActivateSupplier_ReturnsSuccessAndActivates()
    {
        var supplier = CreateSupplier();
        SupplierLifecycle.DeactivateSupplier(supplier, Changed);

        var result = SupplierLifecycle.ActivateSupplier(supplier, Changed);

        Assert.True(result.IsSuccess);
        Assert.True(supplier.IsActive);
    }

    private static Supplier CreateSupplier() =>
        new(EntityId.New(), "Bean Roasters", "0901234567", Created, Created, "sales@beans.com");

    private static void AssertValidation(Result result, string target)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("inventory.invalid_input", result.Error.Code);
        Assert.Equal(target, Assert.Single(result.Error.Details).Target);
    }
}
