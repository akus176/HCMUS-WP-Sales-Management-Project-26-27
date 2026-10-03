namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

// Callers resolve current definitions and compatibility links from persistence before ordering.
public static class CatalogAddons
{
    public static Result<Addon> CreateAddon(EntityId id, string name, decimal? price) =>
        Execute(() => new Addon(id, name, CreatePrice(price)));

    public static Result UpdateAddon(Addon addon, string name, decimal? price) => Execute(() =>
    {
        addon.Update(name, CreatePrice(price));
        return addon;
    });

    public static Result ActivateAddon(Addon addon) => Execute(() =>
    {
        addon.Activate();
        return addon;
    });

    public static Result DeactivateAddon(Addon addon) => Execute(() =>
    {
        addon.Deactivate();
        return addon;
    });

    public static Result<IReadOnlyCollection<CatalogAddonSnapshot>> GetAllowedAddons(
        MenuItem item, Category category, IReadOnlyCollection<Addon> addons, IReadOnlyCollection<MenuItemAddon> compatibility) =>
        Execute<IReadOnlyCollection<CatalogAddonSnapshot>>(() =>
        {
            if (!item.IsAvailableForOrdering(category))
            {
                throw new InvalidOperationException("The menu item is unavailable for ordering.");
            }

            return addons.Where(addon => addon.IsAvailableForOrdering(item, compatibility))
                .Select(addon => new CatalogAddonSnapshot(item, addon, compatibility)).ToList().AsReadOnly();
        });

    private static Money CreatePrice(decimal? price) => price.HasValue
        ? new Money(price.Value)
        : throw new ArgumentException("An add-on requires a price.", nameof(price));

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
