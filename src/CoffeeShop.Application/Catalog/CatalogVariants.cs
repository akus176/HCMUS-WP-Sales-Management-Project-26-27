namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

// Callers supply the complete persisted variant set; the database unique index guards concurrent writes.
public static class CatalogVariants
{
    public static Result<SizeVariant> CreateSizeVariant(EntityId id, MenuItem item, string sizeName, decimal? price, IReadOnlyCollection<SizeVariant> variants) =>
        Execute(() =>
        {
            ArgumentNullException.ThrowIfNull(item);
            var variant = new SizeVariant(id, item.Id, sizeName, CreatePrice(price));
            if (variants.Any(existing => existing.Id == id))
            {
                throw new InvalidOperationException("Size variant identifier already exists.");
            }

            ValidateUniqueSize(item, variant, variant.SizeName, variants);
            return variant;
        });

    public static Result UpdateSizeVariant(MenuItem item, SizeVariant variant, string sizeName, decimal? price, IReadOnlyCollection<SizeVariant> variants) =>
        Execute(() =>
        {
            ValidateOwnership(item, variant);
            var validated = new SizeVariant(variant.Id, item.Id, sizeName, CreatePrice(price));
            ValidateUniqueSize(item, variant, validated.SizeName, variants);
            variant.Update(validated.SizeName, validated.Price);
            return variant;
        });

    public static Result ActivateSizeVariant(SizeVariant variant) => Execute(() =>
    {
        variant.Activate();
        return variant;
    });

    public static Result DeactivateSizeVariant(SizeVariant variant) => Execute(() =>
    {
        variant.Deactivate();
        return variant;
    });

    public static Result<CatalogPriceSnapshot> GetPriceSnapshot(MenuItem item, Category category, EntityId variantId, IReadOnlyCollection<SizeVariant> variants)
    {
        var variant = variants.SingleOrDefault(candidate => candidate.Id == variantId && candidate.MenuItemId == item.Id);
        if (variant is null)
        {
            return Result<CatalogPriceSnapshot>.Failure(Error.NotFound("catalog.variant_not_found", "The selected size variant does not exist for this menu item."));
        }

        return Execute(() =>
        {
            if (!item.IsAvailableForOrdering(category) || !variant.IsAvailableForOrdering(item))
            {
                throw new InvalidOperationException("The selected menu item and size variant are unavailable for ordering.");
            }

            return new CatalogPriceSnapshot(item, variant);
        });
    }

    private static Money CreatePrice(decimal? price) => price.HasValue
        ? new Money(price.Value)
        : throw new ArgumentException("A size variant requires a price.", nameof(price));

    private static void ValidateOwnership(MenuItem item, SizeVariant variant)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(variant);
        if (variant.MenuItemId != item.Id)
        {
            throw new ArgumentException("Size variant does not belong to this menu item.", nameof(variant));
        }
    }

    private static void ValidateUniqueSize(MenuItem item, SizeVariant variant, string sizeName, IReadOnlyCollection<SizeVariant> variants)
    {
        if (variants.Any(existing => existing.MenuItemId == item.Id && existing.Id != variant.Id && existing.SizeName == sizeName))
        {
            throw new InvalidOperationException("The menu item already defines this size.");
        }
    }

    private static Result<T> Execute<T>(Func<T> operation)
        where T : notnull
    {
        try
        {
            return Result<T>.Success(operation());
        }
        catch (ArgumentException exception)
        {
            return Result<T>.Failure(Error.Validation(
                "catalog.invalid_input",
                exception.Message,
                [new ErrorDetail(exception.ParamName ?? "catalog", exception.Message)]));
        }
        catch (InvalidOperationException exception)
        {
            return Result<T>.Failure(Error.Conflict("catalog.invalid_state", exception.Message));
        }
    }
}
