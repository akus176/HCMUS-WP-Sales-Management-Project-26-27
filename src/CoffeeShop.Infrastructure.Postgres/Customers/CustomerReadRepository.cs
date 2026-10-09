namespace CoffeeShop.Infrastructure.Postgres.Customers;

using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerReadRepository : ICustomerReadRepository
{
    private const int RecentOrderLimit = 20;
    private readonly CoffeeShopDbContext context;

    public CustomerReadRepository(CoffeeShopDbContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<CustomerSummary>> SearchCustomersAsync(
        CustomerSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = context.Customers.AsNoTracking();
        if (!criteria.IncludeInactive)
        {
            query = query.Where(customer => customer.IsActive);
        }

        var candidates = await query.ToListAsync(cancellationToken);
        return CustomerSearch.Search(candidates, criteria)
            .Select(customer => new CustomerSummary(
                customer.Id,
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.IsActive,
                customer.LoyaltyPointsBalance))
            .ToArray();
    }

    public async Task<CustomerDetail?> GetCustomerDetailAsync(
        EntityId customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers.AsNoTracking()
            .Include(candidate => candidate.Addresses)
            .FirstOrDefaultAsync(candidate => candidate.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var recentOrders = await context.Orders.AsNoTracking()
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Take(RecentOrderLimit)
            .Select(order => new CustomerOrderSummary(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.Total.Amount,
                order.CreatedAt.Value))
            .ToListAsync(cancellationToken);

        var addresses = customer.Addresses
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.IsActive)
            .ThenBy(address => address.Label)
            .ThenBy(address => address.Id)
            .Select(address => new CustomerAddressSummary(
                address.Id,
                address.AddressLine,
                address.Label,
                address.IsDefault,
                address.IsActive))
            .ToArray();

        return new CustomerDetail(
            customer.Id,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.IsActive,
            customer.LoyaltyPointsBalance,
            customer.CreatedAt.Value,
            customer.UpdatedAt.Value,
            addresses,
            recentOrders);
    }

    public async Task<LoyaltyHistoryPage?> GetLoyaltyHistoryAsync(
        EntityId customerId,
        LoyaltyHistoryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Customers.AsNoTracking().AnyAsync(customer => customer.Id == customerId, cancellationToken))
        {
            return null;
        }

        var query = context.LoyaltyTransactions.AsNoTracking()
            .Where(transaction => transaction.CustomerId == customerId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip(criteria.Offset)
            .Take(criteria.Limit)
            .Select(transaction => new LoyaltyTransactionSummary(
                transaction.Id,
                transaction.Type,
                transaction.Points,
                transaction.BalanceAfter,
                transaction.OrderId,
                transaction.ReversesTransactionId,
                transaction.CreatedAt.Value,
                transaction.CreatedBy))
            .ToListAsync(cancellationToken);
        return new LoyaltyHistoryPage(items, criteria.Offset, criteria.Limit, totalCount);
    }

    public async Task<LoyaltySettingsDetail?> GetLoyaltySettingsAsync(
        CancellationToken cancellationToken = default) =>
        await context.LoyaltySettings.AsNoTracking()
            .Select(settings => new LoyaltySettingsDetail(
                settings.Id,
                settings.EarnPointsPerCurrencyUnit,
                settings.RedeemValuePerPoint,
                settings.MaxRedeemablePercentage,
                settings.UpdatedAt.Value,
                settings.UpdatedBy))
            .SingleOrDefaultAsync(cancellationToken);
}
