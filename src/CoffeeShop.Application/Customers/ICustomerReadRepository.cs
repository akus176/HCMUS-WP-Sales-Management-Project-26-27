namespace CoffeeShop.Application.Customers;

using CoffeeShop.Domain.Common;

public interface ICustomerReadRepository
{
    Task<IReadOnlyList<CustomerSummary>> SearchCustomersAsync(
        CustomerSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<CustomerDetail?> GetCustomerDetailAsync(
        EntityId customerId,
        CancellationToken cancellationToken = default);

    Task<LoyaltyHistoryPage?> GetLoyaltyHistoryAsync(
        EntityId customerId,
        LoyaltyHistoryCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<LoyaltySettingsDetail?> GetLoyaltySettingsAsync(
        CancellationToken cancellationToken = default);
}
