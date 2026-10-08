namespace CoffeeShop.Application.Customers;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;

public interface ICustomerRepository
{
    Task<Customer?> FindCustomerAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<bool> PhoneExistsAsync(
        string normalizedPhone,
        EntityId? excludingCustomerId = null,
        CancellationToken cancellationToken = default);

    Task<Order?> LoadOrderForLoyaltyUpdateAsync(
        EntityId orderId,
        CancellationToken cancellationToken = default);

    Task<Customer?> LoadCustomerForLoyaltyUpdateAsync(
        EntityId customerId,
        CancellationToken cancellationToken = default);

    Task<LoyaltySettings?> LoadLoyaltySettingsForUpdateAsync(
        CancellationToken cancellationToken = default);

    void AddCustomer(Customer customer);

    void AddLoyaltySettings(LoyaltySettings settings);
}
