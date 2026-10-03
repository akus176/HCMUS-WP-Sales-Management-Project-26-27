namespace CoffeeShop.Domain.Tests.Inventory;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class SupplierTests
{
    private static readonly UtcTimestamp Created = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp Changed = new(Created.Value.AddHours(1));

    private static Supplier CreateSupplier(bool isActive = true) =>
        new(EntityId.New(), "Bean Roasters", "0901234567", Created, Created, "sales@beans.com", "12 Roast Street", isActive);

    [Fact]
    public void Constructor_AcceptsValidValuesAndNormalizes()
    {
        var supplier = new Supplier(EntityId.New(), "  Bean Roasters  ", " 0901234567 ", Created, Created, " Sales@Beans.COM ", " 12 Roast Street ");

        Assert.Equal("Bean Roasters", supplier.Name);
        Assert.Equal("0901234567", supplier.Phone);
        Assert.Equal("sales@beans.com", supplier.Email);
        Assert.Equal("12 Roast Street", supplier.Address);
        Assert.True(supplier.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsInvalidName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Supplier(EntityId.New(), name!, "0901234567", Created, Created));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsInvalidPhone(string? phone)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Supplier(EntityId.New(), "Bean Roasters", phone!, Created, Created));
    }

    [Fact]
    public void Update_ChangesContactDataAndStampsUpdatedAt()
    {
        var supplier = CreateSupplier();

        supplier.Update("New Roasters", "0987654321", "new@roasters.com", "99 New Ave", Changed);

        Assert.Equal("New Roasters", supplier.Name);
        Assert.Equal("0987654321", supplier.Phone);
        Assert.Equal("new@roasters.com", supplier.Email);
        Assert.Equal("99 New Ave", supplier.Address);
        Assert.Equal(Changed, supplier.UpdatedAt);
    }

    [Fact]
    public void Update_DoesNotChangeActivationState()
    {
        var supplier = CreateSupplier();
        supplier.Deactivate(Changed);

        supplier.Update("New Roasters", "0987654321", null, null, Changed);

        Assert.False(supplier.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_RejectsInvalidName(string? name)
    {
        var supplier = CreateSupplier();

        Assert.ThrowsAny<ArgumentException>(() => supplier.Update(name!, "0987654321", null, null, Changed));
        Assert.Equal("Bean Roasters", supplier.Name);
        Assert.Equal(Created, supplier.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_RejectsInvalidPhone(string? phone)
    {
        var supplier = CreateSupplier();

        Assert.ThrowsAny<ArgumentException>(() => supplier.Update("New Roasters", phone!, null, null, Changed));
        Assert.Equal("0901234567", supplier.Phone);
        Assert.Equal(Created, supplier.UpdatedAt);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalseAndStampsUpdatedAt()
    {
        var supplier = CreateSupplier();

        supplier.Deactivate(Changed);

        Assert.False(supplier.IsActive);
        Assert.Equal(Changed, supplier.UpdatedAt);
    }

    [Fact]
    public void Deactivate_PreservesHistoricalData()
    {
        var supplier = CreateSupplier();

        supplier.Deactivate(Changed);

        Assert.Equal("Bean Roasters", supplier.Name);
        Assert.Equal("0901234567", supplier.Phone);
        Assert.Equal("sales@beans.com", supplier.Email);
    }

    [Fact]
    public void Activate_SetsIsActiveTrueAndStampsUpdatedAt()
    {
        var supplier = CreateSupplier(isActive: false);

        supplier.Activate(Changed);

        Assert.True(supplier.IsActive);
        Assert.Equal(Changed, supplier.UpdatedAt);
    }
}
