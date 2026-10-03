namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;

public sealed record CatalogPriceSnapshot
{
    public CatalogPriceSnapshot(MenuItem item, SizeVariant variant)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(variant);
        if (!variant.IsAvailableForOrdering(item))
        {
            throw new ArgumentException("The selected menu item and size variant must be active and compatible.", nameof(variant));
        }

        MenuItemId = item.Id;
        SizeVariantId = variant.Id;
        ItemName = item.Name;
        SizeName = variant.SizeName;
        UnitPrice = variant.Price;
    }

    public EntityId MenuItemId { get; }

    public EntityId SizeVariantId { get; }

    public string ItemName { get; }

    public string SizeName { get; }

    public Money UnitPrice { get; }
}
