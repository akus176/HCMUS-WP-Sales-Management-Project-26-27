namespace CoffeeShop.Application.Tests.Catalog;

using System.Globalization;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public class CatalogVariantsTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));

    private static (Category Category, MenuItem Item) CreateItem()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        return (category, new MenuItem(EntityId.New(), category, "Latte", Now, Now));
    }

    [Fact]
    public void CreateAndUpdate_KeepIndependentSizePrices()
    {
        var (category, item) = CreateItem();
        var small = CatalogVariants.CreateSizeVariant(EntityId.New(), item, " Small ", 30000m, []).Value;
        var large = CatalogVariants.CreateSizeVariant(EntityId.New(), item, "Large", 45000m, [small]).Value;

        Assert.True(CatalogVariants.UpdateSizeVariant(item, large, " Large ", 50000m, [small, large]).IsSuccess);
        Assert.Equal(new Money(30000m), small.Price);
        Assert.Equal(new Money(50000m), large.Price);
        var snapshot = CatalogVariants.GetPriceSnapshot(item, category, large.Id, [small, large]).Value;
        Assert.Equal(large.Id, snapshot.SizeVariantId);
        Assert.Equal("Large", snapshot.SizeName);
        Assert.Equal(new Money(50000m), snapshot.UnitPrice);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("10000000000")]
    public void CreateAndUpdate_RejectMissingOrInvalidPriceWithoutMutation(string? value)
    {
        var (_, item) = CreateItem();
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m));
        var price = value is null ? (decimal?)null : decimal.Parse(value, CultureInfo.InvariantCulture);

        var created = CatalogVariants.CreateSizeVariant(EntityId.New(), item, "Small", price, [variant]);
        var updated = CatalogVariants.UpdateSizeVariant(item, variant, "Changed", price, [variant]);

        Assert.Equal(ErrorType.Validation, created.Error!.Type);
        Assert.Equal(ErrorType.Validation, updated.Error!.Type);
        Assert.Single(created.Error.Details);
        Assert.Equal("Large", variant.SizeName);
        Assert.Equal(new Money(45000m), variant.Price);
        Assert.True(variant.IsActive);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("9999999999.99")]
    public void CreateAndUpdate_AcceptPriceStorageBoundaries(string value)
    {
        var (_, item) = CreateItem();
        var price = decimal.Parse(value, CultureInfo.InvariantCulture);
        var result = CatalogVariants.CreateSizeVariant(EntityId.New(), item, "Large", price, []);

        Assert.True(result.IsSuccess);
        Assert.Equal(new Money(price), result.Value.Price);
        Assert.True(CatalogVariants.UpdateSizeVariant(item, result.Value, "Small", price, [result.Value]).IsSuccess);
        Assert.Equal(new Money(price), result.Value.Price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("12345678901234567890123456789012345678901")]
    public void CreateAndUpdate_RejectInvalidSizeNamesWithoutMutation(string? name)
    {
        var (_, item) = CreateItem();
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m));

        var created = CatalogVariants.CreateSizeVariant(EntityId.New(), item, name!, 30000m, [variant]);
        var updated = CatalogVariants.UpdateSizeVariant(item, variant, name!, 30000m, [variant]);

        Assert.Equal(ErrorType.Validation, created.Error!.Type);
        Assert.Equal(ErrorType.Validation, updated.Error!.Type);
        Assert.Equal("Large", variant.SizeName);
        Assert.Equal(new Money(45000m), variant.Price);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateAndUpdate_RejectDuplicateSizeIncludingInactiveVariants(bool isActive)
    {
        var (_, item) = CreateItem();
        var small = new SizeVariant(EntityId.New(), item.Id, "Small", new Money(30000m));
        var large = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m), isActive);

        var created = CatalogVariants.CreateSizeVariant(EntityId.New(), item, " Large ", 50000m, [small, large]);
        var updated = CatalogVariants.UpdateSizeVariant(item, small, " Large ", 50000m, [small, large]);

        Assert.Equal(ErrorType.Conflict, created.Error!.Type);
        Assert.Equal(ErrorType.Conflict, updated.Error!.Type);
        Assert.Equal("Small", small.SizeName);
        Assert.Equal(new Money(30000m), small.Price);
    }

    [Fact]
    public void Create_RejectsDuplicateIdentifierButAllowsSameSizeForAnotherItem()
    {
        var (_, item) = CreateItem();
        var (_, otherItem) = CreateItem();
        var existing = new SizeVariant(EntityId.New(), otherItem.Id, "Large", Money.Zero);

        Assert.Equal(ErrorType.Conflict, CatalogVariants.CreateSizeVariant(existing.Id, item, "Small", 1m, [existing]).Error!.Type);
        Assert.True(CatalogVariants.CreateSizeVariant(EntityId.New(), item, "Large", 45000m, [existing]).IsSuccess);
    }

    [Fact]
    public void Update_RejectsForeignVariantWithoutMutation()
    {
        var (_, item) = CreateItem();
        var variant = new SizeVariant(EntityId.New(), EntityId.New(), "Large", new Money(45000m));

        var result = CatalogVariants.UpdateSizeVariant(item, variant, "Small", 30000m, [variant]);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("Large", variant.SizeName);
        Assert.Equal(new Money(45000m), variant.Price);
    }

    [Fact]
    public void AvailabilityTransitions_MapRepeatedStatesToConflictAndKeepIndependentAvailability()
    {
        var (category, item) = CreateItem();
        var small = new SizeVariant(EntityId.New(), item.Id, "Small", new Money(30000m));
        var large = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m));

        Assert.Equal(ErrorType.Conflict, CatalogVariants.ActivateSizeVariant(large).Error!.Type);
        Assert.True(CatalogVariants.DeactivateSizeVariant(large).IsSuccess);
        Assert.Equal(ErrorType.Conflict, CatalogVariants.DeactivateSizeVariant(large).Error!.Type);
        Assert.True(CatalogVariants.GetPriceSnapshot(item, category, small.Id, [small, large]).IsSuccess);
        Assert.Equal(ErrorType.Conflict, CatalogVariants.GetPriceSnapshot(item, category, large.Id, [small, large]).Error!.Type);
        Assert.True(CatalogVariants.ActivateSizeVariant(large).IsSuccess);
        Assert.True(CatalogVariants.GetPriceSnapshot(item, category, large.Id, [small, large]).IsSuccess);
    }

    [Theory]
    [InlineData("category")]
    [InlineData("item")]
    [InlineData("variant")]
    [InlineData("foreign_category")]
    public void GetPriceSnapshot_RejectsUnavailableSelection(string reason)
    {
        var (category, item) = CreateItem();
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", Money.Zero);
        switch (reason)
        {
            case "category": category.Deactivate(Now); break;
            case "item": item.Deactivate(Now); break;
            case "variant": variant.Deactivate(); break;
            case "foreign_category": category = CreateItem().Category; break;
        }

        var result = CatalogVariants.GetPriceSnapshot(item, category, variant.Id, [variant]);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("missing")]
    [InlineData("foreign")]
    public void GetPriceSnapshot_RejectsMissingOrForeignSize(string reason)
    {
        var (category, item) = CreateItem();
        var variant = new SizeVariant(EntityId.New(), reason == "foreign" ? EntityId.New() : item.Id, "Large", Money.Zero);

        var result = CatalogVariants.GetPriceSnapshot(item, category, reason == "missing" ? EntityId.New() : variant.Id,
            reason == "empty" ? [] : [variant]);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("catalog.variant_not_found", result.Error.Code);
    }

    [Fact]
    public void GetPriceSnapshot_DoesNotChangeWithLaterCatalogEdits()
    {
        var (category, item) = CreateItem();
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m));
        var snapshot = CatalogVariants.GetPriceSnapshot(item, category, variant.Id, [variant]).Value;

        CatalogVariants.UpdateSizeVariant(item, variant, "Extra large", 50000m, [variant]);
        CatalogVariants.DeactivateSizeVariant(variant);
        item.Update("Iced latte", null, category, Now);

        Assert.Equal("Latte", snapshot.ItemName);
        Assert.Equal("Large", snapshot.SizeName);
        Assert.Equal(new Money(45000m), snapshot.UnitPrice);
    }
}
