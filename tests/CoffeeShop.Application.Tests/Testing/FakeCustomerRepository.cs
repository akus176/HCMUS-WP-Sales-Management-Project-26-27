namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;

public sealed class FakeCustomerRepository : ICustomerRepository, ICustomerReadRepository
{
    public Dictionary<EntityId, Customer> Customers { get; } = [];

    public Dictionary<EntityId, Order> Orders { get; } = [];

    public LoyaltySettings? Settings { get; set; }

    public IReadOnlyList<CustomerSummary> SearchResults { get; set; } = [];

    public CustomerDetail? DetailResult { get; set; }

    public LoyaltyHistoryPage? HistoryResult { get; set; }

    public LoyaltySettingsDetail? SettingsResult { get; set; }

    public CustomerSearchCriteria? LastSearchCriteria { get; private set; }

    public LoyaltyHistoryCriteria? LastHistoryCriteria { get; private set; }

    public int OrderLockCount { get; private set; }

    public int CustomerLockCount { get; private set; }

    public int SettingsLockCount { get; private set; }

    public Task<Customer?> FindCustomerAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Customers.GetValueOrDefault(id));

    public Task<bool> PhoneExistsAsync(
        string normalizedPhone,
        EntityId? excludingCustomerId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Customers.Values.Any(customer => customer.Phone == normalizedPhone
            && (excludingCustomerId is null || customer.Id != excludingCustomerId)));

    public Task<Order?> LoadOrderForLoyaltyUpdateAsync(
        EntityId orderId,
        CancellationToken cancellationToken = default)
    {
        OrderLockCount++;
        return Task.FromResult(Orders.GetValueOrDefault(orderId));
    }

    public Task<Customer?> LoadCustomerForLoyaltyUpdateAsync(
        EntityId customerId,
        CancellationToken cancellationToken = default)
    {
        CustomerLockCount++;
        return Task.FromResult(Customers.GetValueOrDefault(customerId));
    }

    public Task<LoyaltySettings?> LoadLoyaltySettingsForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        SettingsLockCount++;
        return Task.FromResult(Settings);
    }

    public void AddCustomer(Customer customer) => Customers.Add(customer.Id, customer);

    public void AddLoyaltySettings(LoyaltySettings settings) => Settings = settings;

    public Task<IReadOnlyList<CustomerSummary>> SearchCustomersAsync(
        CustomerSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        LastSearchCriteria = criteria;
        return Task.FromResult(SearchResults);
    }

    public Task<CustomerDetail?> GetCustomerDetailAsync(
        EntityId customerId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DetailResult?.Id == customerId ? DetailResult : null);

    public Task<LoyaltyHistoryPage?> GetLoyaltyHistoryAsync(
        EntityId customerId,
        LoyaltyHistoryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        LastHistoryCriteria = criteria;
        return Task.FromResult(HistoryResult);
    }

    public Task<LoyaltySettingsDetail?> GetLoyaltySettingsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SettingsResult);
}
