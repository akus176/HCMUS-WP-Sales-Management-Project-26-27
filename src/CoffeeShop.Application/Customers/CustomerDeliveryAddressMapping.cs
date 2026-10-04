namespace CoffeeShop.Application.Customers;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Customers;

/// <summary>
/// Extension methods for projecting CustomerAddress domain entities to the Sales module's CustomerDeliveryAddressReadModel.
/// </summary>
public static class CustomerDeliveryAddressMapping
{
    public static CustomerDeliveryAddressReadModel ToReadModel(this CustomerAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return new(
            address.Id,
            address.CustomerId,
            address.AddressLine,
            address.Label,
            address.IsDefault,
            address.IsActive);
    }

    public static IReadOnlyList<CustomerDeliveryAddressReadModel> ToReadModels(this IEnumerable<CustomerAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        return addresses.Select(ToReadModel).ToList().AsReadOnly();
    }
}
