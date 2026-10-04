namespace CoffeeShop.Application.Customers;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

/// <summary>
/// In-memory implementation of ICustomerDeliveryAddressReader using resolved Customer aggregates.
/// </summary>
public sealed class InMemoryCustomerDeliveryAddressReader : ICustomerDeliveryAddressReader
{
    private readonly Func<IEnumerable<Customer>> _customersProvider;

    public InMemoryCustomerDeliveryAddressReader(Func<IEnumerable<Customer>> customersProvider)
    {
        ArgumentNullException.ThrowIfNull(customersProvider);
        _customersProvider = customersProvider;
    }

    public InMemoryCustomerDeliveryAddressReader(params Customer[] customers)
        : this(() => customers)
    {
    }

    public CustomerDeliveryAddressReadModel? GetAddress(EntityId customerId, EntityId addressId)
    {
        ArgumentNullException.ThrowIfNull(customerId);
        ArgumentNullException.ThrowIfNull(addressId);

        return _customersProvider()
            .FirstOrDefault(c => c.Id == customerId)?
            .Addresses
            .FirstOrDefault(a => a.Id == addressId)?
            .ToReadModel();
    }

    public CustomerDeliveryAddressReadModel? GetAddressById(EntityId addressId)
    {
        ArgumentNullException.ThrowIfNull(addressId);

        return _customersProvider()
            .SelectMany(c => c.Addresses)
            .FirstOrDefault(a => a.Id == addressId)?
            .ToReadModel();
    }

    public IReadOnlyList<CustomerDeliveryAddressReadModel> GetActiveAddresses(EntityId customerId)
    {
        ArgumentNullException.ThrowIfNull(customerId);

        return _customersProvider()
            .FirstOrDefault(c => c.Id == customerId)?
            .Addresses
            .Where(a => a.IsActive)
            .Select(a => a.ToReadModel())
            .ToList() ?? [];
    }

    public CustomerDeliveryAddressReadModel? GetDefaultAddress(EntityId customerId)
    {
        ArgumentNullException.ThrowIfNull(customerId);

        return _customersProvider()
            .FirstOrDefault(c => c.Id == customerId)?
            .Addresses
            .FirstOrDefault(a => a.IsActive && a.IsDefault)?
            .ToReadModel();
    }
}
