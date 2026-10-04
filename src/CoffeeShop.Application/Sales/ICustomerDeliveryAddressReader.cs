namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;

/// <summary>
/// Read contract interface used by the sales module to query and validate customer delivery addresses.
/// </summary>
public interface ICustomerDeliveryAddressReader
{
    /// <summary>
    /// Gets a customer address by customer ID and address ID, enforcing customer boundary ownership.
    /// </summary>
    CustomerDeliveryAddressReadModel? GetAddress(EntityId customerId, EntityId addressId);

    /// <summary>
    /// Gets a customer address by its address ID regardless of customer ID (for ownership verification).
    /// </summary>
    CustomerDeliveryAddressReadModel? GetAddressById(EntityId addressId);

    /// <summary>
    /// Gets all active delivery addresses for the specified customer.
    /// </summary>
    IReadOnlyList<CustomerDeliveryAddressReadModel> GetActiveAddresses(EntityId customerId);

    /// <summary>
    /// Gets the default active delivery address for the specified customer, if one exists.
    /// </summary>
    CustomerDeliveryAddressReadModel? GetDefaultAddress(EntityId customerId);
}
